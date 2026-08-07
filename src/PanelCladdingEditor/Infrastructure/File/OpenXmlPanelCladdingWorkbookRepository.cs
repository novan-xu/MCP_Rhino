using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

public sealed class OpenXmlPanelCladdingWorkbookRepository : IPanelCladdingWorkbookRepository
{
    private const string IndexSheetName = "_CLADDING_INDEX";

    public OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request)
    {
        OperationResponse<string> pathValidation = ValidatePath(request.WorkbookPath, request.AllowCreate);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail(pathValidation.Message);
        }

        string finalPath = pathValidation.Data;
        bool existed = File.Exists(finalPath);
        OperationResponse lockCheck = CheckExclusiveAccess(finalPath, existed);
        if (!lockCheck.Success)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail(lockCheck.Message);
        }

        string directory = Path.GetDirectoryName(finalPath)!;
        string tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(finalPath)}.cladding.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            if (existed)
            {
                File.Copy(finalPath, tempPath, overwrite: false);
            }
            else
            {
                CreateEmptyWorkbook(tempPath);
            }

            long originalLength = existed ? new FileInfo(finalPath).Length : -1L;
            DateTime originalWriteUtc = existed ? File.GetLastWriteTimeUtc(finalPath) : DateTime.MinValue;
            PreparedState state = UpdateTemporaryWorkbook(tempPath, finalPath, request);
            ValidateWorkbook(tempPath);
            return OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Ok(
                new PreparedWorkbookUpdate(
                    tempPath,
                    finalPath,
                    existed,
                    originalLength,
                    originalWriteUtc,
                    state.Result,
                    state.Modified));
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail(
                $"PANEL_CLADDING_WORKBOOK_PREPARE_FAILED: {ex.Message}");
        }
    }

    public OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
        PanelCladdingWorkbookBatchUpsert request)
    {
        PanelCladdingWorkbookUpsert[] items = (request.Items ?? Array.Empty<PanelCladdingWorkbookUpsert>())
            .ToArray();
        if (items.Length == 0)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail(
                "PANEL_CLADDING_WORKBOOK_BATCH_ITEMS_REQUIRED");
        }
        if (items.Any(item => item.Layout.ObjectId == Guid.Empty) ||
            items.Select(item => item.Layout.ObjectId).Distinct().Count() != items.Length)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail(
                "PANEL_CLADDING_WORKBOOK_BATCH_OBJECT_IDS_INVALID");
        }

        OperationResponse<string> pathValidation = ValidatePath(
            request.WorkbookPath,
            request.AllowCreate);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail(
                pathValidation.Message);
        }

        string finalPath = pathValidation.Data;
        bool existed = File.Exists(finalPath);
        OperationResponse lockCheck = CheckExclusiveAccess(finalPath, existed);
        if (!lockCheck.Success)
        {
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail(lockCheck.Message);
        }

        string directory = Path.GetDirectoryName(finalPath)!;
        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(finalPath)}.cladding-batch.{Guid.NewGuid():N}.tmp.xlsx");
        try
        {
            if (existed)
            {
                File.Copy(finalPath, tempPath, overwrite: false);
            }
            else
            {
                CreateEmptyWorkbook(tempPath);
            }

            long originalLength = existed ? new FileInfo(finalPath).Length : -1L;
            DateTime originalWriteUtc = existed ? File.GetLastWriteTimeUtc(finalPath) : DateTime.MinValue;
            var results = new List<PanelCladdingWorkbookBatchItemResult>(items.Length);
            bool modified = false;
            foreach (PanelCladdingWorkbookUpsert item in items)
            {
                PreparedState state = UpdateTemporaryWorkbook(tempPath, finalPath, item);
                results.Add(new PanelCladdingWorkbookBatchItemResult
                {
                    ObjectId = item.Layout.ObjectId,
                    Result = state.Result
                });
                modified |= state.Modified;
            }

            ValidateWorkbook(tempPath);
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Ok(
                new PreparedWorkbookBatchUpdate(
                    tempPath,
                    finalPath,
                    existed,
                    originalLength,
                    originalWriteUtc,
                    results,
                    modified));
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail(
                $"PANEL_CLADDING_WORKBOOK_BATCH_PREPARE_FAILED: {ex.Message}");
        }
    }

    private static PreparedState UpdateTemporaryWorkbook(
        string tempPath,
        string finalPath,
        PanelCladdingWorkbookUpsert request)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(tempPath, true);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("WorkbookPart is missing.");
        workbookPart.Workbook ??= new S.Workbook();
        S.Sheets sheets = workbookPart.Workbook.GetFirstChild<S.Sheets>()
            ?? workbookPart.Workbook.AppendChild(new S.Sheets());
        WorksheetPart indexPart = EnsureIndexSheet(workbookPart, sheets);
        IReadOnlyList<IndexRecord> records = ReadIndex(workbookPart, indexPart);

        IndexRecord? sameSignature = records.FirstOrDefault(record =>
            string.Equals(record.FullDigest, request.Identity.FullDigest, StringComparison.OrdinalIgnoreCase));
        if (sameSignature is not null)
        {
            S.Sheet? existingSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
                string.Equals(sheet.Name?.Value, sameSignature.SheetName, StringComparison.OrdinalIgnoreCase));
            if (existingSheet is null)
            {
                throw new InvalidDataException(
                    $"PANEL_CLADDING_INDEXED_SHEET_MISSING: {sameSignature.SheetName} is indexed but absent.");
            }

            PanelCladdingTypeIdentity reusedIdentity = CloneIdentity(request.Identity, sameSignature.TypeCode);
            return new PreparedState(
                new PanelCladdingWorkbookCommitResult
                {
                    WorkbookPath = finalPath,
                    SheetName = sameSignature.SheetName,
                    Identity = reusedIdentity,
                    ReusedExistingType = true
                },
                Modified: false);
        }

        PanelCladdingTypeIdentity finalIdentity = ResolveTypeCodeCollision(request.Identity, records);
        string sheetName = ResolveSheetName(finalIdentity.TypeCode, sheets);
        WorksheetPart typePart = workbookPart.AddNewPart<WorksheetPart>();
        uint styleIndex = EnsurePanelCellStyle(workbookPart);
        typePart.Worksheet = BuildTypeWorksheet(
            workbookPart,
            typePart,
            request.Layout,
            finalIdentity,
            request.PreviewPng,
            styleIndex);
        AddSheet(workbookPart, sheets, typePart, sheetName, hidden: false);
        AppendIndexRecord(indexPart, new IndexRecord
        {
            TypeCode = finalIdentity.TypeCode,
            FullDigest = finalIdentity.FullDigest,
            StoredSignature = finalIdentity.StoredSignature,
            SheetName = sheetName,
            GeometryClass = request.Layout.GeometryClass.ToString(),
            Grid = $"{request.Layout.ColumnCount}x{request.Layout.RowCount}",
            WidthMillimeters = request.Layout.Width * request.Layout.ModelUnitScaleToMillimeters,
            HeightMillimeters = request.Layout.Height * request.Layout.ModelUnitScaleToMillimeters,
            UpdatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });
        EnsureVisibleActiveSheet(workbookPart, sheets);
        workbookPart.Workbook.Save();

        return new PreparedState(new PanelCladdingWorkbookCommitResult
        {
            WorkbookPath = finalPath,
            SheetName = sheetName,
            Identity = finalIdentity,
            ReusedExistingType = false
        }, Modified: true);
    }

    private static S.Worksheet BuildTypeWorksheet(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart,
        PanelCladdingLayout layout,
        PanelCladdingTypeIdentity identity,
        byte[] previewPng,
        uint styleIndex)
    {
        var worksheet = new S.Worksheet();
        var columns = new S.Columns();
        columns.Append(new S.Column { Min = 1U, Max = 1U, Width = 16d, CustomWidth = true });
        double[] xBoundaries = new[] { 0d }
            .Concat(layout.VerticalOffsets)
            .Concat(new[] { layout.Width })
            .ToArray();
        for (int column = 0; column < layout.ColumnCount; column++)
        {
            double cellWidth = xBoundaries[column + 1] - xBoundaries[column];
            double ratio = cellWidth / Math.Max(layout.Width, 1e-9d);
            columns.Append(new S.Column
            {
                Min = (uint)(column + 2),
                Max = (uint)(column + 2),
                Width = Math.Clamp(8d + ratio * 40d, 10d, 28d),
                CustomWidth = true
            });
        }
        worksheet.Append(columns);

        var sheetData = new S.SheetData();
        sheetData.Append(CreateRow(1U, ("A", "Type Code"), ("B", identity.TypeCode)));
        sheetData.Append(CreateRow(2U, ("A", "Geometry"), ("B", layout.GeometryClass.ToString())));
        sheetData.Append(CreateRow(
            3U,
            ("A", "Size (mm)"),
            ("B", $"{layout.Width * layout.ModelUnitScaleToMillimeters:0.###} x {layout.Height * layout.ModelUnitScaleToMillimeters:0.###}")));
        sheetData.Append(CreateRow(4U, ("A", "Orientation"), ("B", "Rows bottom-to-top A..; columns left-to-right 0..")));

        var headerRow = new S.Row { RowIndex = 6U };
        headerRow.Append(CreateInlineStringCell("A6", "Row"));
        for (int column = 0; column < layout.ColumnCount; column++)
        {
            headerRow.Append(CreateInlineStringCell(GetCellReference(column + 2, 6), column.ToString(CultureInfo.InvariantCulture)));
        }
        sheetData.Append(headerRow);

        double[] yBoundaries = new[] { 0d }
            .Concat(layout.HorizontalOffsets)
            .Concat(new[] { layout.Height })
            .ToArray();
        for (int displayRow = 0; displayRow < layout.RowCount; displayRow++)
        {
            int logicalRow = layout.RowCount - 1 - displayRow;
            uint excelRow = (uint)(7 + displayRow);
            double cellHeight = yBoundaries[logicalRow + 1] - yBoundaries[logicalRow];
            double ratio = cellHeight / Math.Max(layout.Height, 1e-9d);
            var row = new S.Row
            {
                RowIndex = excelRow,
                Height = Math.Clamp(28d + ratio * 120d, 32d, 86d),
                CustomHeight = true
            };
            string rowLabel = PanelCladdingKeyService.GetRowLabel(logicalRow);
            row.Append(CreateInlineStringCell($"A{excelRow}", rowLabel));
            for (int column = 0; column < layout.ColumnCount; column++)
            {
                PanelCladdingCell cell = layout.Cells.Single(item => item.Column == column && item.Row == logicalRow);
                identity.NormalizedCellValues.TryGetValue(cell.UserTextKey, out string? value);
                S.Cell excelCell = CreateInlineStringCell(GetCellReference(column + 2, (int)excelRow), value ?? string.Empty);
                excelCell.StyleIndex = styleIndex;
                row.Append(excelCell);
            }
            sheetData.Append(row);
        }

        uint metadataRow = (uint)(8 + layout.RowCount);
        sheetData.Append(CreateRow(metadataRow, ("A", "H offsets"), ("B", string.Join(", ", layout.HorizontalOffsets.Select(value => value.ToString("0.######", CultureInfo.InvariantCulture))))));
        sheetData.Append(CreateRow(metadataRow + 1, ("A", "V offsets"), ("B", string.Join(", ", layout.VerticalOffsets.Select(value => value.ToString("0.######", CultureInfo.InvariantCulture))))));
        sheetData.Append(CreateRow(metadataRow + 2, ("A", "Signature"), ("B", identity.StoredSignature)));
        worksheet.Append(sheetData);
        if (layout.ColumnCount > 1)
        {
            string metadataEndColumn = GetColumnLetters(layout.ColumnCount + 3);
            worksheet.Append(new S.MergeCells(
                new S.MergeCell { Reference = $"B4:{metadataEndColumn}4" },
                new S.MergeCell { Reference = $"B{metadataRow + 2}:{metadataEndColumn}{metadataRow + 2}" }));
        }
        AddPreviewImage(worksheetPart, worksheet, previewPng, layout.ColumnCount + 3, 1);
        return worksheet;
    }

    private static void AddPreviewImage(
        WorksheetPart worksheetPart,
        S.Worksheet worksheet,
        byte[] png,
        int startColumn,
        int startRow)
    {
        DrawingsPart drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        ImagePart imagePart = drawingsPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(png, writable: false))
        {
            imagePart.FeedData(stream);
        }

        string imageRelationshipId = drawingsPart.GetIdOfPart(imagePart);
        var picture = new Xdr.Picture(
            new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = 1U, Name = "Panel axonometric preview" },
                new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new Xdr.BlipFill(
                new A.Blip { Embed = imageRelationshipId, CompressionState = A.BlipCompressionValues.Print },
                new A.Stretch(new A.FillRectangle())),
            new Xdr.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = 7_620_000L, Cy = 5_248_000L }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        var anchor = new Xdr.OneCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId(startColumn.ToString(CultureInfo.InvariantCulture)),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId(startRow.ToString(CultureInfo.InvariantCulture)),
                new Xdr.RowOffset("0")),
            new Xdr.Extent { Cx = 7_620_000L, Cy = 5_248_000L },
            picture,
            new Xdr.ClientData());
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(anchor);
        drawingsPart.WorksheetDrawing.Save();
        worksheet.Append(new S.Drawing { Id = worksheetPart.GetIdOfPart(drawingsPart) });
    }

    private static WorksheetPart EnsureIndexSheet(WorkbookPart workbookPart, S.Sheets sheets)
    {
        S.Sheet? sheet = sheets.Elements<S.Sheet>().FirstOrDefault(item =>
            string.Equals(item.Name?.Value, IndexSheetName, StringComparison.OrdinalIgnoreCase));
        if (sheet is not null)
        {
            return (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        }

        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        var data = new S.SheetData();
        data.Append(CreateRow(
            1U,
            ("A", "TypeCode"),
            ("B", "FullDigest"),
            ("C", "StoredSignature"),
            ("D", "SheetName"),
            ("E", "GeometryClass"),
            ("F", "Grid"),
            ("G", "WidthMm"),
            ("H", "HeightMm"),
            ("I", "UpdatedUtc")));
        part.Worksheet = new S.Worksheet(data);
        part.Worksheet.Save();
        AddSheet(workbookPart, sheets, part, IndexSheetName, hidden: true);
        return part;
    }

    private static IReadOnlyList<IndexRecord> ReadIndex(WorkbookPart workbookPart, WorksheetPart indexPart)
    {
        var records = new List<IndexRecord>();
        S.Worksheet worksheet = indexPart.Worksheet
            ?? throw new InvalidDataException("Cladding index worksheet is missing.");
        S.SheetData? data = worksheet.GetFirstChild<S.SheetData>();
        if (data is null)
        {
            return records;
        }

        foreach (S.Row row in data.Elements<S.Row>().Skip(1))
        {
            var values = row.Elements<S.Cell>()
                .ToDictionary(
                    cell => GetColumnIndex(cell.CellReference?.Value),
                    cell => ReadCellText(workbookPart, cell));
            string typeCode = Get(values, 1);
            string digest = Get(values, 2);
            if (string.IsNullOrWhiteSpace(typeCode) || string.IsNullOrWhiteSpace(digest))
            {
                continue;
            }

            records.Add(new IndexRecord
            {
                TypeCode = typeCode,
                FullDigest = digest,
                StoredSignature = Get(values, 3),
                SheetName = Get(values, 4),
                GeometryClass = Get(values, 5),
                Grid = Get(values, 6),
                WidthMillimeters = ParseDouble(Get(values, 7)),
                HeightMillimeters = ParseDouble(Get(values, 8)),
                UpdatedUtc = Get(values, 9)
            });
        }
        return records;
    }

    private static void AppendIndexRecord(WorksheetPart indexPart, IndexRecord record)
    {
        S.Worksheet worksheet = indexPart.Worksheet
            ?? throw new InvalidDataException("Cladding index worksheet is missing.");
        S.SheetData data = worksheet.GetFirstChild<S.SheetData>()
            ?? worksheet.AppendChild(new S.SheetData());
        uint rowIndex = data.Elements<S.Row>().Select(row => row.RowIndex?.Value ?? 0U).DefaultIfEmpty().Max() + 1U;
        data.Append(CreateRow(
            rowIndex,
            ("A", record.TypeCode),
            ("B", record.FullDigest),
            ("C", record.StoredSignature),
            ("D", record.SheetName),
            ("E", record.GeometryClass),
            ("F", record.Grid),
            ("G", record.WidthMillimeters.ToString("0.########", CultureInfo.InvariantCulture)),
            ("H", record.HeightMillimeters.ToString("0.########", CultureInfo.InvariantCulture)),
            ("I", record.UpdatedUtc)));
        worksheet.Save();
    }

    private static PanelCladdingTypeIdentity ResolveTypeCodeCollision(
        PanelCladdingTypeIdentity identity,
        IReadOnlyList<IndexRecord> records)
    {
        int digestLength = 8;
        string candidate = identity.TypeCode;
        while (records.Any(record => string.Equals(record.TypeCode, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            digestLength += 2;
            if (digestLength > 20)
            {
                throw new InvalidDataException("PANEL_CLADDING_TYPE_CODE_COLLISION_EXHAUSTED");
            }
            candidate = PanelCladdingTypeSignatureService.WithDigestLength(identity, digestLength);
        }
        return CloneIdentity(identity, candidate);
    }

    private static PanelCladdingTypeIdentity CloneIdentity(PanelCladdingTypeIdentity identity, string typeCode)
    {
        return new PanelCladdingTypeIdentity
        {
            SchemaVersion = identity.SchemaVersion,
            TypeCode = typeCode,
            FullDigest = identity.FullDigest,
            StoredSignature = identity.StoredSignature,
            CanonicalPayload = identity.CanonicalPayload,
            NormalizedCellValues = identity.NormalizedCellValues
        };
    }

    private static uint EnsurePanelCellStyle(WorkbookPart workbookPart)
    {
        WorkbookStylesPart stylesPart = workbookPart.WorkbookStylesPart
            ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        if (stylesPart.Stylesheet is null)
        {
            stylesPart.Stylesheet = CreateBaseStylesheet();
        }

        S.Stylesheet stylesheet = stylesPart.Stylesheet;
        stylesheet.Fonts ??= new S.Fonts(new S.Font()) { Count = 1U };
        stylesheet.Fills ??= new S.Fills(
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2U };
        stylesheet.Borders ??= new S.Borders(new S.Border()) { Count = 1U };
        stylesheet.CellStyleFormats ??= new S.CellStyleFormats(new S.CellFormat()) { Count = 1U };
        stylesheet.CellFormats ??= new S.CellFormats(new S.CellFormat()) { Count = 1U };

        uint fillId = stylesheet.Fills.Count?.Value ?? (uint)stylesheet.Fills.ChildElements.Count;
        var pattern = new S.PatternFill { PatternType = S.PatternValues.Solid };
        pattern.ForegroundColor = new S.ForegroundColor { Rgb = HexBinaryValue.FromString("FFDDEBF7") };
        pattern.BackgroundColor = new S.BackgroundColor { Indexed = 64U };
        stylesheet.Fills.Append(new S.Fill(pattern));
        stylesheet.Fills.Count = fillId + 1U;

        uint borderId = stylesheet.Borders.Count?.Value ?? (uint)stylesheet.Borders.ChildElements.Count;
        stylesheet.Borders.Append(new S.Border(
            new S.LeftBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = HexBinaryValue.FromString("FF4472C4") } },
            new S.RightBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = HexBinaryValue.FromString("FF4472C4") } },
            new S.TopBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = HexBinaryValue.FromString("FF4472C4") } },
            new S.BottomBorder { Style = S.BorderStyleValues.Thin, Color = new S.Color { Rgb = HexBinaryValue.FromString("FF4472C4") } },
            new S.DiagonalBorder()));
        stylesheet.Borders.Count = borderId + 1U;

        uint styleIndex = stylesheet.CellFormats.Count?.Value ?? (uint)stylesheet.CellFormats.ChildElements.Count;
        stylesheet.CellFormats.Append(new S.CellFormat
        {
            FontId = 0U,
            FillId = fillId,
            BorderId = borderId,
            ApplyFill = true,
            ApplyBorder = true,
            ApplyAlignment = true,
            Alignment = new S.Alignment { Horizontal = S.HorizontalAlignmentValues.Center, Vertical = S.VerticalAlignmentValues.Center, WrapText = true }
        });
        stylesheet.CellFormats.Count = styleIndex + 1U;
        stylesheet.Save();
        return styleIndex;
    }

    private static S.Stylesheet CreateBaseStylesheet()
    {
        return new S.Stylesheet(
            new S.Fonts(new S.Font()) { Count = 1U },
            new S.Fills(
                new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
                new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2U },
            new S.Borders(new S.Border()) { Count = 1U },
            new S.CellStyleFormats(new S.CellFormat()) { Count = 1U },
            new S.CellFormats(new S.CellFormat()) { Count = 1U },
            new S.CellStyles(new S.CellStyle { Name = "Normal", FormatId = 0U, BuiltinId = 0U }) { Count = 1U },
            new S.DifferentialFormats { Count = 0U },
            new S.TableStyles { Count = 0U, DefaultTableStyle = "TableStyleMedium2", DefaultPivotStyle = "PivotStyleLight16" });
    }

    private static S.Row CreateRow(uint rowIndex, params (string Column, string Value)[] values)
    {
        var row = new S.Row { RowIndex = rowIndex };
        foreach ((string column, string value) in values)
        {
            row.Append(CreateInlineStringCell($"{column}{rowIndex}", value));
        }
        return row;
    }

    private static S.Cell CreateInlineStringCell(string reference, string value)
    {
        return new S.Cell
        {
            CellReference = reference,
            DataType = S.CellValues.InlineString,
            InlineString = new S.InlineString(new S.Text(value ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })
        };
    }

    private static void AddSheet(
        WorkbookPart workbookPart,
        S.Sheets sheets,
        WorksheetPart worksheetPart,
        string name,
        bool hidden)
    {
        uint id = sheets.Elements<S.Sheet>().Select(sheet => sheet.SheetId?.Value ?? 0U).DefaultIfEmpty().Max() + 1U;
        var sheet = new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = id,
            Name = name
        };
        if (hidden)
        {
            sheet.State = S.SheetStateValues.Hidden;
        }
        sheets.Append(sheet);
    }

    private static string ResolveSheetName(string requested, S.Sheets sheets)
    {
        string sanitized = requested;
        foreach (char invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
        {
            sanitized = sanitized.Replace(invalid, '_');
        }
        sanitized = sanitized.Length > 31 ? sanitized[..31] : sanitized;
        if (!sheets.Elements<S.Sheet>().Any(sheet => string.Equals(sheet.Name?.Value, sanitized, StringComparison.OrdinalIgnoreCase)))
        {
            return sanitized;
        }

        for (int suffix = 2; suffix < 1000; suffix++)
        {
            string ending = $"-{suffix}";
            string prefix = sanitized.Length + ending.Length > 31 ? sanitized[..(31 - ending.Length)] : sanitized;
            string candidate = prefix + ending;
            if (!sheets.Elements<S.Sheet>().Any(sheet => string.Equals(sheet.Name?.Value, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
        throw new InvalidDataException("PANEL_CLADDING_SHEET_NAME_EXHAUSTED");
    }

    private static void EnsureVisibleActiveSheet(WorkbookPart workbookPart, S.Sheets sheets)
    {
        int visibleIndex = sheets.Elements<S.Sheet>().ToList().FindIndex(sheet => sheet.State?.Value != S.SheetStateValues.Hidden);
        if (visibleIndex < 0)
        {
            return;
        }
        S.Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("Workbook is missing.");
        workbook.BookViews ??= new S.BookViews();
        S.WorkbookView view = workbook.BookViews.Elements<S.WorkbookView>().FirstOrDefault()
            ?? workbook.BookViews.AppendChild(new S.WorkbookView());
        view.ActiveTab = (uint)visibleIndex;
    }

    private static void CreateEmptyWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new S.Workbook(new S.Sheets());
        workbookPart.Workbook.Save();
    }

    private static OperationResponse<string> ValidatePath(string rawPath, bool allowCreate)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_PATH_REQUIRED");
        }

        string path;
        try
        {
            path = Path.GetFullPath(rawPath);
        }
        catch (Exception ex)
        {
            return OperationResponse<string>.Fail($"PANEL_CLADDING_WORKBOOK_PATH_INVALID: {ex.Message}");
        }

        if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_XLSX_REQUIRED");
        }
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_DIRECTORY_NOT_FOUND");
        }
        if (!File.Exists(path) && !allowCreate)
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_WORKBOOK_NOT_FOUND");
        }
        return OperationResponse<string>.Ok(path);
    }

    private static OperationResponse CheckExclusiveAccess(string path, bool exists)
    {
        if (!exists)
        {
            return OperationResponse.Ok();
        }
        try
        {
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return OperationResponse.Ok();
        }
        catch (Exception ex)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_WORKBOOK_LOCKED: close the workbook and retry. {ex.Message}");
        }
    }

    private static void ValidateWorkbook(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        var validator = new OpenXmlValidator(FileFormatVersions.Office2016);
        IReadOnlyList<ValidationErrorInfo> errors = validator.Validate(document).Take(20).ToArray();
        if (errors.Count > 0)
        {
            string summary = string.Join(" | ", errors.Select(error => error.Description));
            throw new InvalidDataException($"PANEL_CLADDING_WORKBOOK_INVALID: {summary}");
        }
    }

    private static string ReadCellText(WorkbookPart workbookPart, S.Cell cell)
    {
        if (cell.DataType?.Value == S.CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }
        if (cell.DataType?.Value == S.CellValues.SharedString &&
            int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable?.ElementAtOrDefault(index)?.InnerText ?? string.Empty;
        }
        return cell.CellValue?.Text ?? string.Empty;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }
        int result = 0;
        foreach (char character in cellReference.TakeWhile(char.IsLetter))
        {
            result = result * 26 + (char.ToUpperInvariant(character) - 'A' + 1);
        }
        return result;
    }

    private static string GetCellReference(int columnNumber, int rowNumber)
    {
        return GetColumnLetters(columnNumber) + rowNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetColumnLetters(int columnNumber)
    {
        string letters = string.Empty;
        int current = columnNumber;
        while (current > 0)
        {
            current--;
            letters = (char)('A' + current % 26) + letters;
            current /= 26;
        }
        return letters;
    }

    private static string Get(IReadOnlyDictionary<int, string> values, int column) =>
        values.TryGetValue(column, out string? value) ? value : string.Empty;

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0d;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort for an operation-owned temporary file only.
        }
    }

    private sealed class PreparedWorkbookUpdate : IPreparedPanelCladdingWorkbookUpdate
    {
        private readonly string _tempPath;
        private readonly string _finalPath;
        private readonly bool _existed;
        private readonly long _originalLength;
        private readonly DateTime _originalWriteUtc;
        private readonly bool _modified;
        private bool _committed;

        public PreparedWorkbookUpdate(
            string tempPath,
            string finalPath,
            bool existed,
            long originalLength,
            DateTime originalWriteUtc,
            PanelCladdingWorkbookCommitResult result,
            bool modified)
        {
            _tempPath = tempPath;
            _finalPath = finalPath;
            _existed = existed;
            _originalLength = originalLength;
            _originalWriteUtc = originalWriteUtc;
            Result = result;
            _modified = modified;
        }

        public PanelCladdingWorkbookCommitResult Result { get; }

        public OperationResponse Commit()
        {
            if (_committed)
            {
                return OperationResponse.Ok("Workbook update already committed.");
            }

            try
            {
                if (!_modified)
                {
                    TryDelete(_tempPath);
                    _committed = true;
                    return OperationResponse.Ok("Existing workbook type reused.");
                }

                if (_existed)
                {
                    var current = new FileInfo(_finalPath);
                    if (!current.Exists || current.Length != _originalLength || current.LastWriteTimeUtc != _originalWriteUtc)
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CHANGED_DURING_SAVE");
                    }
                    string backup = _finalPath + ".mcp-cladding-backup";
                    TryDelete(backup);
                    File.Replace(_tempPath, _finalPath, backup, ignoreMetadataErrors: true);
                    TryDelete(backup);
                }
                else
                {
                    if (File.Exists(_finalPath))
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CREATED_DURING_SAVE");
                    }
                    File.Move(_tempPath, _finalPath);
                }

                _committed = true;
                return OperationResponse.Ok("Panel cladding workbook updated.");
            }
            catch (Exception ex)
            {
                return OperationResponse.Fail($"PANEL_CLADDING_WORKBOOK_COMMIT_FAILED: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_committed)
            {
                TryDelete(_tempPath);
            }
        }
    }

    private sealed class PreparedWorkbookBatchUpdate : IPreparedPanelCladdingWorkbookBatchUpdate
    {
        private readonly string _tempPath;
        private readonly string _finalPath;
        private readonly bool _existed;
        private readonly long _originalLength;
        private readonly DateTime _originalWriteUtc;
        private readonly bool _modified;
        private bool _committed;

        public PreparedWorkbookBatchUpdate(
            string tempPath,
            string finalPath,
            bool existed,
            long originalLength,
            DateTime originalWriteUtc,
            IReadOnlyList<PanelCladdingWorkbookBatchItemResult> results,
            bool modified)
        {
            _tempPath = tempPath;
            _finalPath = finalPath;
            _existed = existed;
            _originalLength = originalLength;
            _originalWriteUtc = originalWriteUtc;
            Results = results;
            _modified = modified;
        }

        public IReadOnlyList<PanelCladdingWorkbookBatchItemResult> Results { get; }

        public OperationResponse Commit()
        {
            if (_committed)
            {
                return OperationResponse.Ok("Workbook batch update already committed.");
            }

            try
            {
                if (!_modified)
                {
                    TryDelete(_tempPath);
                    _committed = true;
                    return OperationResponse.Ok("All workbook types already exist.");
                }

                if (_existed)
                {
                    var current = new FileInfo(_finalPath);
                    if (!current.Exists ||
                        current.Length != _originalLength ||
                        current.LastWriteTimeUtc != _originalWriteUtc)
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CHANGED_DURING_SAVE");
                    }
                    string backup = _finalPath + ".mcp-cladding-backup";
                    TryDelete(backup);
                    File.Replace(_tempPath, _finalPath, backup, ignoreMetadataErrors: true);
                    TryDelete(backup);
                }
                else
                {
                    if (File.Exists(_finalPath))
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CREATED_DURING_SAVE");
                    }
                    File.Move(_tempPath, _finalPath);
                }

                _committed = true;
                return OperationResponse.Ok("Panel cladding workbook batch updated.");
            }
            catch (Exception ex)
            {
                return OperationResponse.Fail(
                    $"PANEL_CLADDING_WORKBOOK_BATCH_COMMIT_FAILED: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_committed)
            {
                TryDelete(_tempPath);
            }
        }
    }

    private sealed record PreparedState(PanelCladdingWorkbookCommitResult Result, bool Modified);

    private sealed class IndexRecord
    {
        public string TypeCode { get; init; } = string.Empty;
        public string FullDigest { get; init; } = string.Empty;
        public string StoredSignature { get; init; } = string.Empty;
        public string SheetName { get; init; } = string.Empty;
        public string GeometryClass { get; init; } = string.Empty;
        public string Grid { get; init; } = string.Empty;
        public double WidthMillimeters { get; init; }
        public double HeightMillimeters { get; init; }
        public string UpdatedUtc { get; init; } = string.Empty;
    }
}

