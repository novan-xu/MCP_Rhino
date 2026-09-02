using System.Globalization;
using System.Text.RegularExpressions;
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
    private const string MaterialSheetName = "Materials";
    private const string FrameExtrusionSheetName = "Extrusions";

    public OperationResponse<PanelCladdingMaterialCatalog> ReadMaterialCatalog(string workbookPath)
    {
        OperationResponse<string> pathValidation = ValidatePath(workbookPath, allowCreate: false);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<PanelCladdingMaterialCatalog>.Fail(pathValidation.Message);
        }
        try
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Open(pathValidation.Data, false);
            WorkbookPart workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("WorkbookPart is missing.");
            S.Sheets sheets = workbookPart.Workbook?.GetFirstChild<S.Sheets>()
                ?? throw new InvalidDataException("Workbook sheets are missing.");
            S.Sheet? materialSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
                string.Equals(sheet.Name?.Value, MaterialSheetName, StringComparison.OrdinalIgnoreCase));
            if (materialSheet is not null)
            {
                WorksheetPart part = (WorksheetPart)workbookPart.GetPartById(materialSheet.Id!);
                return OperationResponse<PanelCladdingMaterialCatalog>.Ok(new PanelCladdingMaterialCatalog
                {
                    WorkbookPath = pathValidation.Data,
                    Materials = ReadMaterialRows(workbookPart, part),
                    UsesLegacyTypeFallback = false
                });
            }

            return OperationResponse<PanelCladdingMaterialCatalog>.Ok(new PanelCladdingMaterialCatalog
            {
                WorkbookPath = pathValidation.Data,
                Materials = ReadLegacyMaterialRows(workbookPart, sheets),
                UsesLegacyTypeFallback = true
            }, "Legacy cladding type sheets were scanned; save Material Setup to migrate the catalogue.");
        }
        catch (Exception ex)
        {
            return OperationResponse<PanelCladdingMaterialCatalog>.Fail(
                $"PANEL_CLADDING_MATERIAL_CATALOG_READ_FAILED: {ex.Message}");
        }
    }

    public OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate> PrepareMaterialCatalog(
        PanelCladdingMaterialCatalogSaveRequest request)
    {
        OperationResponse<string> pathValidation = ValidatePath(request.WorkbookPath, request.AllowCreate);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Fail(pathValidation.Message);
        }
        OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>> validated =
            ValidateMaterialCatalog(request.Materials);
        if (!validated.Success || validated.Data is null)
        {
            return OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Fail(validated.Message);
        }

        string finalPath = pathValidation.Data;
        bool existed = File.Exists(finalPath);
        OperationResponse lockCheck = CheckExclusiveAccess(finalPath, existed);
        if (!lockCheck.Success)
        {
            return OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Fail(lockCheck.Message);
        }
        string directory = Path.GetDirectoryName(finalPath)!;
        string tempPath = Path.Combine(directory,
            $".{Path.GetFileNameWithoutExtension(finalPath)}.materials.{Guid.NewGuid():N}.tmp.xlsx");
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
            int removed = WriteMaterialCatalog(tempPath, validated.Data, request.RemoveLegacyTypeSheets);
            ValidateWorkbook(tempPath);
            return OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Ok(
                new PreparedMaterialCatalogUpdate(
                    tempPath,
                    finalPath,
                    existed,
                    originalLength,
                    originalWriteUtc,
                    new PanelCladdingMaterialCatalogSaveResult
                    {
                        WorkbookPath = finalPath,
                        MaterialCount = validated.Data.Count,
                        RemovedLegacyTypeSheetCount = removed
                    }));
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Fail(
                $"PANEL_CLADDING_MATERIAL_CATALOG_PREPARE_FAILED: {ex.Message}");
        }
    }

    public OperationResponse<PanelFrameExtrusionCatalog> ReadFrameExtrusionCatalog(string workbookPath)
    {
        OperationResponse<string> pathValidation = ValidatePath(workbookPath, allowCreate: false);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<PanelFrameExtrusionCatalog>.Fail(pathValidation.Message);
        }
        try
        {
            using SpreadsheetDocument document = SpreadsheetDocument.Open(pathValidation.Data, false);
            WorkbookPart workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("WorkbookPart is missing.");
            S.Sheets sheets = workbookPart.Workbook?.GetFirstChild<S.Sheets>()
                ?? throw new InvalidDataException("Workbook sheets are missing.");
            S.Sheet? extrusionSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
                string.Equals(sheet.Name?.Value, FrameExtrusionSheetName, StringComparison.OrdinalIgnoreCase));
            return OperationResponse<PanelFrameExtrusionCatalog>.Ok(new PanelFrameExtrusionCatalog
            {
                WorkbookPath = pathValidation.Data,
                Extrusions = extrusionSheet is null
                    ? Array.Empty<PanelFrameExtrusionCatalogItem>()
                    : ReadFrameExtrusionRows(
                        workbookPart,
                        (WorksheetPart)workbookPart.GetPartById(extrusionSheet.Id!))
            });
        }
        catch (Exception exception)
        {
            return OperationResponse<PanelFrameExtrusionCatalog>.Fail(
                $"PANEL_FRAME_EXTRUSION_CATALOG_READ_FAILED: {exception.Message}");
        }
    }

    public OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate> PrepareFrameExtrusionCatalog(
        PanelFrameExtrusionCatalogSaveRequest request)
    {
        OperationResponse<string> pathValidation = ValidatePath(request.WorkbookPath, request.AllowCreate);
        if (!pathValidation.Success || pathValidation.Data is null)
        {
            return OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Fail(pathValidation.Message);
        }
        OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>> validated =
            ValidateFrameExtrusionCatalog(request.Extrusions);
        if (!validated.Success || validated.Data is null)
        {
            return OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Fail(validated.Message);
        }

        string finalPath = pathValidation.Data;
        bool existed = File.Exists(finalPath);
        OperationResponse lockCheck = CheckExclusiveAccess(finalPath, existed);
        if (!lockCheck.Success)
        {
            return OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Fail(lockCheck.Message);
        }
        string directory = Path.GetDirectoryName(finalPath)!;
        string tempPath = Path.Combine(directory,
            $".{Path.GetFileNameWithoutExtension(finalPath)}.extrusions.{Guid.NewGuid():N}.tmp.xlsx");
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
            WriteFrameExtrusionCatalog(tempPath, validated.Data);
            ValidateWorkbook(tempPath);
            return OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Ok(
                new PreparedFrameExtrusionCatalogUpdate(
                    tempPath,
                    finalPath,
                    existed,
                    originalLength,
                    originalWriteUtc,
                    new PanelFrameExtrusionCatalogSaveResult
                    {
                        WorkbookPath = finalPath,
                        ExtrusionCount = validated.Data.Count
                    }));
        }
        catch (Exception exception)
        {
            TryDelete(tempPath);
            return OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Fail(
                $"PANEL_FRAME_EXTRUSION_CATALOG_PREPARE_FAILED: {exception.Message}");
        }
    }

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
        if (items.Length == 0 && !request.PruneUnusedTypes)
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
            bool modified = !existed;
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

            PanelCladdingWorkbookTypeReference[] retainedTypes = (request.RetainedTypes ??
                    Array.Empty<PanelCladdingWorkbookTypeReference>())
                .Concat(results.Select(item => new PanelCladdingWorkbookTypeReference
                {
                    ObjectId = item.ObjectId,
                    TypeCode = item.Result.Identity.TypeCode,
                    StoredSignature = item.Result.Identity.StoredSignature
                }))
                .ToArray();
            IReadOnlyList<string> removedTypeCodes = request.PruneUnusedTypes
                ? PruneUnusedManagedTypes(tempPath, retainedTypes)
                : Array.Empty<string>();
            modified |= removedTypeCodes.Count > 0;

            ValidateWorkbook(tempPath);
            return OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Ok(
                new PreparedWorkbookBatchUpdate(
                    tempPath,
                    finalPath,
                    existed,
                    originalLength,
                    originalWriteUtc,
                    results,
                    removedTypeCodes,
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
        sheetData.Append(CreateRow(metadataRow, ("A", "H offsets"), ("B", string.Join(", ", layout.HorizontalOffsets.Select(PanelCladdingKeyService.FormatOffset)))));
        sheetData.Append(CreateRow(metadataRow + 1, ("A", "V offsets"), ("B", string.Join(", ", layout.VerticalOffsets.Select(PanelCladdingKeyService.FormatOffset)))));
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

    private static IReadOnlyList<string> PruneUnusedManagedTypes(
        string tempPath,
        IReadOnlyList<PanelCladdingWorkbookTypeReference> retainedTypes)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(tempPath, true);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("WorkbookPart is missing.");
        workbookPart.Workbook ??= new S.Workbook();
        S.Sheets sheets = workbookPart.Workbook.GetFirstChild<S.Sheets>()
            ?? workbookPart.Workbook.AppendChild(new S.Sheets());
        S.Sheet? indexSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, IndexSheetName, StringComparison.OrdinalIgnoreCase));
        if (indexSheet is null)
        {
            EnsureAtLeastOneVisibleSheet(workbookPart, sheets);
            workbookPart.Workbook.Save();
            return Array.Empty<string>();
        }

        var retainedTypeCodes = (retainedTypes ?? Array.Empty<PanelCladdingWorkbookTypeReference>())
            .Select(reference => reference.TypeCode.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var retainedSignatures = (retainedTypes ?? Array.Empty<PanelCladdingWorkbookTypeReference>())
            .Select(reference => reference.StoredSignature.Trim())
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        WorksheetPart indexPart = (WorksheetPart)workbookPart.GetPartById(indexSheet.Id!);
        IReadOnlyList<IndexRecord> records = ReadIndex(workbookPart, indexPart);
        IndexRecord[] removed = records.Where(record =>
                !retainedTypeCodes.Contains(record.TypeCode) &&
                !retainedSignatures.Contains(record.StoredSignature))
            .ToArray();
        if (removed.Length == 0)
        {
            return Array.Empty<string>();
        }

        foreach (IndexRecord record in removed)
        {
            S.Sheet? managedSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
                !string.Equals(sheet.Name?.Value, IndexSheetName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(sheet.Name?.Value, record.SheetName, StringComparison.OrdinalIgnoreCase));
            if (managedSheet is null)
            {
                continue;
            }
            OpenXmlPart managedPart = workbookPart.GetPartById(managedSheet.Id!);
            managedSheet.Remove();
            workbookPart.DeletePart(managedPart);
        }

        RewriteIndex(indexPart, records.Except(removed).ToArray());
        EnsureAtLeastOneVisibleSheet(workbookPart, sheets);
        EnsureVisibleActiveSheet(workbookPart, sheets);
        workbookPart.Workbook.Save();
        return removed
            .Select(record => record.TypeCode)
            .Where(typeCode => !string.IsNullOrWhiteSpace(typeCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(typeCode => typeCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void RewriteIndex(WorksheetPart indexPart, IReadOnlyList<IndexRecord> records)
    {
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
        uint rowIndex = 2U;
        foreach (IndexRecord record in records)
        {
            data.Append(CreateRow(
                rowIndex++,
                ("A", record.TypeCode),
                ("B", record.FullDigest),
                ("C", record.StoredSignature),
                ("D", record.SheetName),
                ("E", record.GeometryClass),
                ("F", record.Grid),
                ("G", record.WidthMillimeters.ToString("0.########", CultureInfo.InvariantCulture)),
                ("H", record.HeightMillimeters.ToString("0.########", CultureInfo.InvariantCulture)),
                ("I", record.UpdatedUtc)));
        }
        indexPart.Worksheet = new S.Worksheet(data);
        indexPart.Worksheet.Save();
    }

    private static void EnsureAtLeastOneVisibleSheet(WorkbookPart workbookPart, S.Sheets sheets)
    {
        if (sheets.Elements<S.Sheet>().Any(sheet => sheet.State?.Value != S.SheetStateValues.Hidden))
        {
            return;
        }

        WorksheetPart summaryPart = workbookPart.AddNewPart<WorksheetPart>();
        summaryPart.Worksheet = new S.Worksheet(new S.SheetData(CreateRow(
            1U,
            ("A", "No cladding types are currently assigned in the Rhino model."))));
        summaryPart.Worksheet.Save();
        AddSheet(
            workbookPart,
            sheets,
            summaryPart,
            ResolveSheetName("Cladding Types", sheets),
            hidden: false);
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

    private static OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>> ValidateMaterialCatalog(
        IReadOnlyList<PanelCladdingMaterialCatalogItem>? materials)
    {
        var validated = new List<PanelCladdingMaterialCatalogItem>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingMaterialCatalogItem item in materials ?? Array.Empty<PanelCladdingMaterialCatalogItem>())
        {
            string code = item.Code.Trim().ToUpperInvariant();
            string description = item.Description.Trim();
            string category = item.Category.Trim();
            string color = item.ColorHex.Trim().ToUpperInvariant();
            if (code.Length == 0 || description.Length == 0 || category.Length == 0)
            {
                return OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>>.Fail(
                    "PANEL_CLADDING_MATERIAL_FIELDS_REQUIRED");
            }
            if (!Regex.IsMatch(color, "^#[0-9A-F]{6}$", RegexOptions.CultureInvariant))
            {
                return OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>>.Fail(
                    $"PANEL_CLADDING_MATERIAL_COLOR_INVALID: {code} color must be #RRGGBB.");
            }
            if (!codes.Add(code))
            {
                return OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>>.Fail(
                    $"PANEL_CLADDING_MATERIAL_CODE_DUPLICATE: {code}");
            }
            validated.Add(new PanelCladdingMaterialCatalogItem
            {
                Code = code,
                Description = description,
                Category = category,
                ColorHex = color
            });
        }
        return OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>>.Ok(
            validated.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>> ValidateFrameExtrusionCatalog(
        IReadOnlyList<PanelFrameExtrusionCatalogItem>? extrusions)
    {
        var validated = new List<PanelFrameExtrusionCatalogItem>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelFrameExtrusionCatalogItem item in extrusions ?? Array.Empty<PanelFrameExtrusionCatalogItem>())
        {
            string sourceCode = item.SourceCode.Trim().ToUpperInvariant();
            string baseCode = string.IsNullOrWhiteSpace(item.BaseCode)
                ? (sourceCode.StartsWith("ALU-", StringComparison.Ordinal) ? sourceCode[4..] : sourceCode)
                : item.BaseCode.Trim().ToUpperInvariant();
            string code = item.Dimension is null
                ? string.Empty
                : $"{(item.Dimension == PanelFrameProfileDimension.OneDimensional ? "1D" : "0D")}-{baseCode}";
            string description = item.Description.Trim();
            string sourcePdf = item.SourcePdfPath.Trim();
            string category = string.IsNullOrWhiteSpace(item.Category)
                ? $"PAGE {item.SourcePageNumber}"
                : item.Category.Trim().ToUpperInvariant();
            string parent = item.ParentCode.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(baseCode, "^[A-Z0-9][A-Z0-9-]{0,76}$", RegexOptions.CultureInvariant) ||
                !Regex.IsMatch(sourceCode, "^[A-Z0-9][A-Z0-9-]{0,80}$", RegexOptions.CultureInvariant) ||
                (!string.IsNullOrEmpty(parent) &&
                 !Regex.IsMatch(parent, "^(?:0D|1D)-[A-Z0-9][A-Z0-9-]{0,76}$", RegexOptions.CultureInvariant)))
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Fail(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_CODE_INVALID: {sourceCode}");
            }
            if (item.SourcePageNumber <= 0 || item.ThumbnailPng.Length == 0 ||
                item.ThumbnailPng.Length > 24_000 || !HasPngSignature(item.ThumbnailPng))
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Fail(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_THUMBNAIL_INVALID: {code}");
            }
            if (!sources.Add(sourceCode) || (!string.IsNullOrEmpty(code) && !codes.Add(code)))
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Fail(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_CODE_DUPLICATE: {sourceCode}");
            }
            double? calculationValue = item.CalculationValue;
            PanelFrameProfileCalculation calculation = item.Dimension == PanelFrameProfileDimension.OneDimensional
                ? PanelFrameProfileCalculation.Length
                : item.Calculation;
            if (item.Dimension == PanelFrameProfileDimension.OneDimensional)
            {
                calculationValue ??= 1d;
                if (!IsPositiveInteger(calculationValue.Value))
                {
                    return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Fail(
                        $"PANEL_FRAME_EXTRUSION_CATALOG_QUANTITY_INVALID: {code}");
                }
            }
            else if (item.Dimension == PanelFrameProfileDimension.ZeroDimensional &&
                     (calculation == PanelFrameProfileCalculation.Length || calculationValue is null ||
                      !double.IsFinite(calculationValue.Value) || calculationValue.Value <= 0d ||
                      (calculation == PanelFrameProfileCalculation.FixedQuantity &&
                       !IsPositiveInteger(calculationValue.Value))))
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Fail(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_VALUE_INVALID: {code}");
            }
            validated.Add(new PanelFrameExtrusionCatalogItem
            {
                Code = code,
                BaseCode = baseCode,
                SourceCode = sourceCode,
                Description = description,
                Category = category,
                Dimension = item.Dimension,
                Calculation = calculation,
                CalculationValue = calculationValue,
                ParentCode = parent,
                SourcePdfPath = sourcePdf,
                SourcePageNumber = item.SourcePageNumber,
                ThumbnailPng = item.ThumbnailPng.ToArray()
            });
        }
        return OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>>.Ok(
            validated.OrderBy(item => item.SourcePageNumber)
                .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.BaseCode, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IReadOnlyList<PanelFrameExtrusionCatalogItem> ReadFrameExtrusionRows(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart)
    {
        S.SheetData? data = worksheetPart.Worksheet?.GetFirstChild<S.SheetData>();
        if (data is null)
        {
            return Array.Empty<PanelFrameExtrusionCatalogItem>();
        }
        S.Row? headerRow = data.Elements<S.Row>().FirstOrDefault();
        var headers = headerRow?.Elements<S.Cell>().ToDictionary(
            cell => GetColumnIndex(cell.CellReference?.Value),
            cell => ReadCellText(workbookPart, cell)) ?? new Dictionary<int, string>();
        bool version2 = string.Equals(Get(headers, 2), "Base Code", StringComparison.OrdinalIgnoreCase);
        var result = new List<PanelFrameExtrusionCatalogItem>();
        foreach (S.Row row in data.Elements<S.Row>().Skip(1))
        {
            var values = row.Elements<S.Cell>().ToDictionary(
                cell => GetColumnIndex(cell.CellReference?.Value),
                cell => ReadCellText(workbookPart, cell));
            string code = Get(values, 1).Trim();
            string sourceCode = Get(values, version2 ? 3 : 2).Trim();
            if (sourceCode.Length == 0)
            {
                continue;
            }
            int pageColumn = version2 ? 11 : 5;
            int thumbnailColumn = version2 ? 12 : 6;
            if (!int.TryParse(Get(values, pageColumn), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pageNumber))
            {
                throw new InvalidDataException($"PANEL_FRAME_EXTRUSION_CATALOG_PAGE_INVALID: {code}");
            }
            byte[] thumbnail;
            try
            {
                thumbnail = Convert.FromBase64String(Get(values, thumbnailColumn));
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_THUMBNAIL_INVALID: {code}",
                    exception);
            }
            OperationResponse<IReadOnlyList<PanelFrameExtrusionCatalogItem>> validated =
                ValidateFrameExtrusionCatalog(
                [
                    new PanelFrameExtrusionCatalogItem
                    {
                        Code = code,
                        BaseCode = version2 ? Get(values, 2) : string.Empty,
                        SourceCode = sourceCode,
                        Description = Get(values, version2 ? 4 : 3),
                        Category = version2 ? Get(values, 5) : string.Empty,
                        Dimension = version2 ? ParseDimension(Get(values, 6)) : PanelFrameProfileDimension.OneDimensional,
                        Calculation = version2 ? ParseCalculation(Get(values, 7)) : PanelFrameProfileCalculation.Length,
                        CalculationValue = version2 ? ParseOptionalDouble(Get(values, 8)) : 1d,
                        ParentCode = version2 ? Get(values, 9) : string.Empty,
                        SourcePdfPath = Get(values, version2 ? 10 : 4),
                        SourcePageNumber = pageNumber,
                        ThumbnailPng = thumbnail
                    }
                ]);
            if (!validated.Success || validated.Data is null)
            {
                throw new InvalidDataException(validated.Message);
            }
            result.Add(validated.Data[0]);
        }
        if (result.Select(item => item.SourceCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
        {
            throw new InvalidDataException("PANEL_FRAME_EXTRUSION_CATALOG_CODE_DUPLICATE");
        }
        return result.OrderBy(item => item.SourcePageNumber)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BaseCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PanelFrameProfileDimension? ParseDimension(string value) => value.Trim().ToUpperInvariant() switch
    {
        "1D" => PanelFrameProfileDimension.OneDimensional,
        "0D" => PanelFrameProfileDimension.ZeroDimensional,
        "" => null,
        _ => throw new InvalidDataException($"PANEL_FRAME_EXTRUSION_CATALOG_DIMENSION_INVALID: {value}")
    };

    private static PanelFrameProfileCalculation ParseCalculation(string value) => value.Trim().ToUpperInvariant() switch
    {
        "" or "LENGTH" => PanelFrameProfileCalculation.Length,
        "FIXED" => PanelFrameProfileCalculation.FixedQuantity,
        "SPACING" => PanelFrameProfileCalculation.Spacing,
        _ => throw new InvalidDataException($"PANEL_FRAME_EXTRUSION_CATALOG_CALCULATION_INVALID: {value}")
    };

    private static double? ParseOptionalDouble(string value) => string.IsNullOrWhiteSpace(value)
        ? null
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : throw new InvalidDataException($"PANEL_FRAME_EXTRUSION_CATALOG_VALUE_INVALID: {value}");

    private static bool IsPositiveInteger(double value) =>
        double.IsFinite(value) && value >= 1d && value <= 100_000d && Math.Abs(value - Math.Round(value)) < 1e-9d;

    private static bool HasPngSignature(IReadOnlyList<byte> bytes) =>
        bytes.Count >= 8 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static IReadOnlyList<PanelCladdingMaterialCatalogItem> ReadMaterialRows(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart)
    {
        S.SheetData? data = worksheetPart.Worksheet?.GetFirstChild<S.SheetData>();
        if (data is null)
        {
            return Array.Empty<PanelCladdingMaterialCatalogItem>();
        }
        var result = new List<PanelCladdingMaterialCatalogItem>();
        foreach (S.Row row in data.Elements<S.Row>().Skip(1))
        {
            var values = row.Elements<S.Cell>().ToDictionary(
                cell => GetColumnIndex(cell.CellReference?.Value),
                cell => ReadCellText(workbookPart, cell));
            string code = Get(values, 1).Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                continue;
            }
            string description = Get(values, 2).Trim();
            string category = Get(values, 3).Trim();
            string color = Get(values, 4).Trim().ToUpperInvariant();
            OperationResponse<IReadOnlyList<PanelCladdingMaterialCatalogItem>> validated = ValidateMaterialCatalog(
                [new PanelCladdingMaterialCatalogItem
                {
                    Code = code,
                    Description = description,
                    Category = category,
                    ColorHex = color
                }]);
            if (!validated.Success || validated.Data is null)
            {
                throw new InvalidDataException(validated.Message);
            }
            result.Add(validated.Data[0]);
        }
        if (result.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
        {
            throw new InvalidDataException("PANEL_CLADDING_MATERIAL_CODE_DUPLICATE");
        }
        return result.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<PanelCladdingMaterialCatalogItem> ReadLegacyMaterialRows(
        WorkbookPart workbookPart,
        S.Sheets sheets)
    {
        S.Sheet? indexSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, IndexSheetName, StringComparison.OrdinalIgnoreCase));
        if (indexSheet is null)
        {
            return Array.Empty<PanelCladdingMaterialCatalogItem>();
        }
        WorksheetPart indexPart = (WorksheetPart)workbookPart.GetPartById(indexSheet.Id!);
        HashSet<string> managedNames = ReadIndex(workbookPart, indexPart)
            .Select(record => record.SheetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (S.Sheet sheet in sheets.Elements<S.Sheet>().Where(sheet => managedNames.Contains(sheet.Name?.Value ?? string.Empty)))
        {
            WorksheetPart part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            foreach (S.Cell cell in part.Worksheet?.Descendants<S.Cell>() ?? Enumerable.Empty<S.Cell>())
            {
                string value = ReadCellText(workbookPart, cell).Trim().ToUpperInvariant();
                if (Regex.IsMatch(value, "^[A-Z]{2,4}-[0-9]{3}$", RegexOptions.CultureInvariant))
                {
                    codes.Add(value);
                }
            }
        }
        return codes.Select(CreateDefaultMaterialCatalogItem)
            .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static PanelCladdingMaterialCatalogItem CreateDefaultMaterialCatalogItem(string code)
    {
        string family = PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(code);
        string category = family switch
        {
            "Surfaces-Glass" => "Glass",
            "Surfaces-Stone" => "Stone",
            "Surfaces-Terracotta" => "Terracotta",
            "Surfaces-Metal" => "Metal",
            "Surfaces-Concrete" => "Concrete",
            "Surfaces-Wood" => "Wood",
            _ => "Composite"
        };
        PanelColorRgb color = PanelCladdingSpawnPlanningService.ResolveMaterialLayerColor(code);
        return new PanelCladdingMaterialCatalogItem
        {
            Code = code,
            Description = category switch
            {
                "Glass" => "Glazing panel",
                "Stone" => "Stone finish",
                "Terracotta" => "Terracotta finish",
                "Metal" => "Metal panel",
                _ => $"{category} finish"
            },
            Category = category,
            ColorHex = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
        };
    }

    private static void WriteFrameExtrusionCatalog(
        string tempPath,
        IReadOnlyList<PanelFrameExtrusionCatalogItem> extrusions)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(tempPath, true);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("WorkbookPart is missing.");
        workbookPart.Workbook ??= new S.Workbook();
        S.Sheets sheets = workbookPart.Workbook.GetFirstChild<S.Sheets>()
            ?? workbookPart.Workbook.AppendChild(new S.Sheets());
        S.Sheet? prior = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, FrameExtrusionSheetName, StringComparison.OrdinalIgnoreCase));
        if (prior is not null)
        {
            DeleteSheet(workbookPart, prior);
        }

        WorksheetPart part = workbookPart.AddNewPart<WorksheetPart>();
        part.Worksheet = BuildFrameExtrusionWorksheet(extrusions);
        part.Worksheet.Save();
        AddSheet(workbookPart, sheets, part, FrameExtrusionSheetName, hidden: false);
        EnsureVisibleActiveSheet(workbookPart, sheets);
        workbookPart.Workbook.Save();
    }

    private static S.Worksheet BuildFrameExtrusionWorksheet(
        IReadOnlyList<PanelFrameExtrusionCatalogItem> extrusions)
    {
        var view = new S.SheetView
        {
            WorkbookViewId = 0U,
            ShowGridLines = false
        };
        view.Append(new S.Pane
        {
            VerticalSplit = 1D,
            TopLeftCell = "A2",
            ActivePane = S.PaneValues.BottomLeft,
            State = S.PaneStateValues.Frozen
        });
        var columns = new S.Columns(
            new S.Column { Min = 1U, Max = 3U, Width = 18d, CustomWidth = true },
            new S.Column { Min = 4U, Max = 5U, Width = 28d, CustomWidth = true },
            new S.Column { Min = 6U, Max = 9U, Width = 14d, CustomWidth = true },
            new S.Column { Min = 10U, Max = 10U, Width = 54d, CustomWidth = true },
            new S.Column { Min = 11U, Max = 11U, Width = 10d, CustomWidth = true },
            new S.Column { Min = 12U, Max = 12U, Width = 2d, CustomWidth = true, Hidden = true });
        var data = new S.SheetData();
        S.Row header = CreateRow(1U,
            ("A", "Extrusion Code"),
            ("B", "Base Code"),
            ("C", "Source Code"),
            ("D", "Description"),
            ("E", "Category"),
            ("F", "Dimension"),
            ("G", "Calculation"),
            ("H", "Value"),
            ("I", "Parent Code"),
            ("J", "Source PDF"),
            ("K", "Page"),
            ("L", "Thumbnail PNG Base64"));
        header.Height = 25d;
        header.CustomHeight = true;
        data.Append(header);
        uint rowIndex = 2U;
        foreach (PanelFrameExtrusionCatalogItem extrusion in extrusions)
        {
            S.Row row = CreateRow(rowIndex,
                ("A", extrusion.Code),
                ("B", extrusion.BaseCode),
                ("C", extrusion.SourceCode),
                ("D", extrusion.Description),
                ("E", extrusion.Category),
                ("F", extrusion.Dimension switch
                {
                    PanelFrameProfileDimension.OneDimensional => "1D",
                    PanelFrameProfileDimension.ZeroDimensional => "0D",
                    _ => string.Empty
                }),
                ("G", extrusion.Calculation switch
                {
                    PanelFrameProfileCalculation.FixedQuantity => "Fixed",
                    PanelFrameProfileCalculation.Spacing => "Spacing",
                    _ => "Length"
                }),
                ("H", extrusion.CalculationValue?.ToString("0.#####", CultureInfo.InvariantCulture) ?? string.Empty),
                ("I", extrusion.ParentCode),
                ("J", extrusion.SourcePdfPath),
                ("K", extrusion.SourcePageNumber.ToString(CultureInfo.InvariantCulture)),
                ("L", Convert.ToBase64String(extrusion.ThumbnailPng)));
            row.Height = 22d;
            row.CustomHeight = true;
            data.Append(row);
            rowIndex++;
        }
        string endReference = $"L{Math.Max(1U, rowIndex - 1U)}";
        return new S.Worksheet(
            new S.SheetViews(view),
            columns,
            data,
            new S.AutoFilter { Reference = $"A1:{endReference}" });
    }

    private static int WriteMaterialCatalog(
        string tempPath,
        IReadOnlyList<PanelCladdingMaterialCatalogItem> materials,
        bool removeLegacyTypeSheets)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(tempPath, true);
        WorkbookPart workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("WorkbookPart is missing.");
        workbookPart.Workbook ??= new S.Workbook();
        S.Sheets sheets = workbookPart.Workbook.GetFirstChild<S.Sheets>()
            ?? workbookPart.Workbook.AppendChild(new S.Sheets());
        S.Sheet? priorMaterials = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, MaterialSheetName, StringComparison.OrdinalIgnoreCase));
        if (priorMaterials is not null)
        {
            DeleteSheet(workbookPart, priorMaterials);
        }

        WorksheetPart materialPart = workbookPart.AddNewPart<WorksheetPart>();
        MaterialStyles styles = EnsureMaterialStyles(workbookPart, materials.Select(item => item.ColorHex));
        materialPart.Worksheet = BuildMaterialWorksheet(materials, styles);
        materialPart.Worksheet.Save();
        AddSheet(workbookPart, sheets, materialPart, MaterialSheetName, hidden: false);

        int removed = removeLegacyTypeSheets ? RemoveLegacyManagedTypeSheets(workbookPart, sheets) : 0;
        EnsureVisibleActiveSheet(workbookPart, sheets);
        workbookPart.Workbook.Save();
        return removed;
    }

    private static S.Worksheet BuildMaterialWorksheet(
        IReadOnlyList<PanelCladdingMaterialCatalogItem> materials,
        MaterialStyles styles)
    {
        var view = new S.SheetView
        {
            WorkbookViewId = 0U,
            ShowGridLines = false
        };
        view.Append(new S.Pane
        {
            VerticalSplit = 1D,
            TopLeftCell = "A2",
            ActivePane = S.PaneValues.BottomLeft,
            State = S.PaneStateValues.Frozen
        });
        var columns = new S.Columns(
            new S.Column { Min = 1U, Max = 1U, Width = 18d, CustomWidth = true },
            new S.Column { Min = 2U, Max = 2U, Width = 32d, CustomWidth = true },
            new S.Column { Min = 3U, Max = 3U, Width = 18d, CustomWidth = true },
            new S.Column { Min = 4U, Max = 4U, Width = 14d, CustomWidth = true },
            new S.Column { Min = 5U, Max = 5U, Width = 11d, CustomWidth = true });
        var data = new S.SheetData();
        S.Row header = CreateRow(1U,
            ("A", "Material Code"),
            ("B", "Description"),
            ("C", "Category"),
            ("D", "Color Hex"),
            ("E", "Preview"));
        header.Height = 25d;
        header.CustomHeight = true;
        foreach (S.Cell cell in header.Elements<S.Cell>())
        {
            cell.StyleIndex = styles.HeaderStyle;
        }
        data.Append(header);
        uint rowIndex = 2U;
        foreach (PanelCladdingMaterialCatalogItem material in materials)
        {
            S.Row row = CreateRow(rowIndex,
                ("A", material.Code),
                ("B", material.Description),
                ("C", material.Category),
                ("D", material.ColorHex),
                ("E", string.Empty));
            row.Height = 22d;
            row.CustomHeight = true;
            row.Elements<S.Cell>().Last().StyleIndex = styles.ColorStyles[material.ColorHex];
            data.Append(row);
            rowIndex++;
        }
        string endReference = $"E{Math.Max(1U, rowIndex - 1U)}";
        return new S.Worksheet(
            new S.SheetViews(view),
            columns,
            data,
            new S.AutoFilter { Reference = $"A1:{endReference}" });
    }

    private static int RemoveLegacyManagedTypeSheets(WorkbookPart workbookPart, S.Sheets sheets)
    {
        S.Sheet? indexSheet = sheets.Elements<S.Sheet>().FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, IndexSheetName, StringComparison.OrdinalIgnoreCase));
        if (indexSheet is null)
        {
            return 0;
        }
        WorksheetPart indexPart = (WorksheetPart)workbookPart.GetPartById(indexSheet.Id!);
        HashSet<string> managedNames = ReadIndex(workbookPart, indexPart)
            .Select(record => record.SheetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        S.Sheet[] managedSheets = sheets.Elements<S.Sheet>()
            .Where(sheet => managedNames.Contains(sheet.Name?.Value ?? string.Empty))
            .ToArray();
        foreach (S.Sheet sheet in managedSheets)
        {
            DeleteSheet(workbookPart, sheet);
        }
        DeleteSheet(workbookPart, indexSheet);
        return managedSheets.Length;
    }

    private static void DeleteSheet(WorkbookPart workbookPart, S.Sheet sheet)
    {
        OpenXmlPart part = workbookPart.GetPartById(sheet.Id!);
        sheet.Remove();
        workbookPart.DeletePart(part);
    }

    private static MaterialStyles EnsureMaterialStyles(
        WorkbookPart workbookPart,
        IEnumerable<string> colors)
    {
        WorkbookStylesPart stylesPart = workbookPart.WorkbookStylesPart
            ?? workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet ??= CreateBaseStylesheet();
        S.Stylesheet stylesheet = stylesPart.Stylesheet;
        stylesheet.Fonts ??= new S.Fonts(new S.Font()) { Count = 1U };
        stylesheet.Fills ??= new S.Fills(
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
            new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2U };
        stylesheet.Borders ??= new S.Borders(new S.Border()) { Count = 1U };
        stylesheet.CellStyleFormats ??= new S.CellStyleFormats(new S.CellFormat()) { Count = 1U };
        stylesheet.CellFormats ??= new S.CellFormats(new S.CellFormat()) { Count = 1U };

        uint fontId = stylesheet.Fonts.Count?.Value ?? (uint)stylesheet.Fonts.ChildElements.Count;
        stylesheet.Fonts.Append(new S.Font(
            new S.Bold(),
            new S.Color { Rgb = HexBinaryValue.FromString("FFFFFFFF") },
            new S.FontName { Val = "Aptos" }));
        stylesheet.Fonts.Count = fontId + 1U;
        uint headerFill = AppendSolidFill(stylesheet, "FF3E494F");
        uint headerStyle = AppendCellFormat(stylesheet, fontId, headerFill, horizontalCenter: false);

        var colorStyles = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (string color in colors.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            uint fill = AppendSolidFill(stylesheet, "FF" + color.TrimStart('#').ToUpperInvariant());
            colorStyles[color] = AppendCellFormat(stylesheet, 0U, fill, horizontalCenter: true);
        }
        stylesheet.Save();
        return new MaterialStyles(headerStyle, colorStyles);
    }

    private static uint AppendSolidFill(S.Stylesheet stylesheet, string argb)
    {
        uint fillId = stylesheet.Fills!.Count?.Value ?? (uint)stylesheet.Fills.ChildElements.Count;
        stylesheet.Fills.Append(new S.Fill(new S.PatternFill(
            new S.ForegroundColor { Rgb = HexBinaryValue.FromString(argb) },
            new S.BackgroundColor { Indexed = 64U })
        { PatternType = S.PatternValues.Solid }));
        stylesheet.Fills.Count = fillId + 1U;
        return fillId;
    }

    private static uint AppendCellFormat(
        S.Stylesheet stylesheet,
        uint fontId,
        uint fillId,
        bool horizontalCenter)
    {
        uint index = stylesheet.CellFormats!.Count?.Value ?? (uint)stylesheet.CellFormats.ChildElements.Count;
        stylesheet.CellFormats.Append(new S.CellFormat
        {
            FontId = fontId,
            FillId = fillId,
            BorderId = 0U,
            ApplyFont = true,
            ApplyFill = true,
            ApplyAlignment = true,
            Alignment = new S.Alignment
            {
                Horizontal = horizontalCenter ? S.HorizontalAlignmentValues.Center : S.HorizontalAlignmentValues.Left,
                Vertical = S.VerticalAlignmentValues.Center
            }
        });
        stylesheet.CellFormats.Count = index + 1U;
        return index;
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

    private sealed record MaterialStyles(
        uint HeaderStyle,
        IReadOnlyDictionary<string, uint> ColorStyles);

    private sealed class PreparedMaterialCatalogUpdate : IPreparedPanelCladdingMaterialCatalogUpdate
    {
        private readonly string _tempPath;
        private readonly string _finalPath;
        private readonly bool _existed;
        private readonly long _originalLength;
        private readonly DateTime _originalWriteUtc;
        private bool _committed;

        public PreparedMaterialCatalogUpdate(
            string tempPath,
            string finalPath,
            bool existed,
            long originalLength,
            DateTime originalWriteUtc,
            PanelCladdingMaterialCatalogSaveResult result)
        {
            _tempPath = tempPath;
            _finalPath = finalPath;
            _existed = existed;
            _originalLength = originalLength;
            _originalWriteUtc = originalWriteUtc;
            Result = result;
        }

        public PanelCladdingMaterialCatalogSaveResult Result { get; }

        public OperationResponse Commit()
        {
            if (_committed)
            {
                return OperationResponse.Ok("Material catalogue update already committed.");
            }
            try
            {
                if (_existed)
                {
                    var current = new FileInfo(_finalPath);
                    if (!current.Exists || current.Length != _originalLength ||
                        current.LastWriteTimeUtc != _originalWriteUtc)
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CHANGED_DURING_SAVE");
                    }
                    string backup = _finalPath + ".panel-materials-backup";
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
                return OperationResponse.Ok("Project material catalogue updated.");
            }
            catch (Exception ex)
            {
                return OperationResponse.Fail($"PANEL_CLADDING_MATERIAL_CATALOG_COMMIT_FAILED: {ex.Message}");
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

    private sealed class PreparedFrameExtrusionCatalogUpdate : IPreparedPanelFrameExtrusionCatalogUpdate
    {
        private readonly string _tempPath;
        private readonly string _finalPath;
        private readonly bool _existed;
        private readonly long _originalLength;
        private readonly DateTime _originalWriteUtc;
        private bool _committed;

        public PreparedFrameExtrusionCatalogUpdate(
            string tempPath,
            string finalPath,
            bool existed,
            long originalLength,
            DateTime originalWriteUtc,
            PanelFrameExtrusionCatalogSaveResult result)
        {
            _tempPath = tempPath;
            _finalPath = finalPath;
            _existed = existed;
            _originalLength = originalLength;
            _originalWriteUtc = originalWriteUtc;
            Result = result;
        }

        public PanelFrameExtrusionCatalogSaveResult Result { get; }

        public OperationResponse Commit()
        {
            if (_committed)
            {
                return OperationResponse.Ok("Frame extrusion catalogue update already committed.");
            }
            try
            {
                if (_existed)
                {
                    var current = new FileInfo(_finalPath);
                    if (!current.Exists || current.Length != _originalLength ||
                        current.LastWriteTimeUtc != _originalWriteUtc)
                    {
                        return OperationResponse.Fail("PANEL_CLADDING_WORKBOOK_CHANGED_DURING_SAVE");
                    }
                    string backup = _finalPath + ".panel-extrusions-backup";
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
                return OperationResponse.Ok("Project frame extrusion catalogue updated.");
            }
            catch (Exception exception)
            {
                return OperationResponse.Fail(
                    $"PANEL_FRAME_EXTRUSION_CATALOG_COMMIT_FAILED: {exception.Message}");
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
            IReadOnlyList<string> removedTypeCodes,
            bool modified)
        {
            _tempPath = tempPath;
            _finalPath = finalPath;
            _existed = existed;
            _originalLength = originalLength;
            _originalWriteUtc = originalWriteUtc;
            Results = results;
            RemovedTypeCodes = removedTypeCodes;
            _modified = modified;
        }

        public IReadOnlyList<PanelCladdingWorkbookBatchItemResult> Results { get; }
        public IReadOnlyList<string> RemovedTypeCodes { get; }

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

