using DocumentFormat.OpenXml.Packaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace PanelCladdingEditor.Smoke;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            string? artifactArgument = args.Length >= 1 ? args[0] : null;
            string? artifactRoot = artifactArgument is null ? null : Path.GetFullPath(artifactArgument);
            RunPanelCladdingTypeEditorSmoke(artifactRoot);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Standalone panel cladding editor smoke failed: {ex}");
            return 1;
        }
    }

    private static void RunPanelCladdingTypeEditorSmoke(string? artifactRoot)
    {
        var checkpoints = new List<string>();
        bool keepArtifacts = !string.IsNullOrWhiteSpace(artifactRoot);
        string output = keepArtifacts
            ? artifactRoot!
            : Path.Combine(Path.GetTempPath(), $"panel-cladding-editor-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);

        try
        {
            var keys = new PanelCladdingKeyService();
            var signatureService = new PanelCladdingTypeSignatureService(keys);
            var projection = new PanelAxonometricProjectionService();
            var renderer = new PanelPreviewRenderer();
            var workbook = new OpenXmlPanelCladdingWorkbookRepository();

            RequirePanelCladding(
                PanelCladdingKeyService.TypeCodeKey == "CW_2.14_CLADDING_TYPE",
                "The cladding type key must use the canonical CW_2.14 schema location.");
            RequirePanelCladding(
                PanelCladdingKeyService.LegacyTypeCodeKey == "CW_4.00_CLADDING_TYPE",
                "The legacy type key must remain explicit for save-time migration.");
            RequirePanelCladding(
                PanelCladdingKeyService.SignatureKey == "Signature",
                "The Rhino-only cladding signature key must be named Signature.");
            RequirePanelCladding(
                PanelCladdingKeyService.LegacySignatureKey == "CW_4.00_CLADDING_SIGNATURE",
                "The legacy signature key must remain explicit for save-time migration.");

            IReadOnlyDictionary<string, string> userText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cw_2.03_offset_h0"] = "50",
                ["CW_2.04_OFFSET_V0"] = "50",
                [PanelCladdingKeyService.GetCellKey(0, "A")] = " gl02 ",
                [PanelCladdingKeyService.GetCellKey(0, "B")] = "ter01",
                [PanelCladdingKeyService.GetCellKey(1, "A")] = "stn02",
                [PanelCladdingKeyService.GetCellKey(1, "B")] = "gl01"
            };
            PanelCladdingKeySet keySet = RequireData(keys.Parse(userText, 100d, 100d, 0.001d), "Parse key grid");
            RequirePanelCladding(keySet.Cells.Count == 4, "Expected four cells for one H and one V cut.");
            RequirePanelCladding(keySet.Cells.Select(item => item.ShortLabel).SequenceEqual(new[] { "0A", "0B", "1A", "1B" }),
                "Cell ordering must be bottom-to-top, then left-to-right.");
            RequirePanelCladding(PanelCladdingKeyService.GetRowLabel(26) == "AA", "Row labels must continue after Z.");

            var gapped = new Dictionary<string, string> { ["CW_2.03_OFFSET_H1"] = "50" };
            OperationResponse<PanelCladdingKeySet> gappedResult = keys.Parse(gapped, 100d, 100d, 0.001d);
            RequirePanelCladding(!gappedResult.Success && gappedResult.Message.Contains("GAPPED_H", StringComparison.Ordinal),
                "Gapped H indices must fail explicitly.");
            checkpoints.Add("canonical assignment keys, parsing, ordering, normalization, and hard-error validation");

            PanelCladdingLayout planar = BuildSmokeLayout(
                projection, keySet, PanelMesh(planar: true, duplicateDepth: false), 1d, 0.001d);
            PanelCladdingLayout curved = BuildSmokeLayout(
                projection, keySet, PanelMesh(planar: false, duplicateDepth: false), 1d, 0.001d);
            PanelCladdingLayout unsupported = BuildSmokeLayout(
                projection, keySet, PanelMesh(planar: true, duplicateDepth: true), 1d, 0.001d);
            RequirePanelCladding(planar.GeometryClass == PanelGeometryClass.Planar, "Flat mesh was not classified Planar.");
            RequirePanelCladding(curved.GeometryClass == PanelGeometryClass.Curved, "Single-valued bowed mesh was not classified Curved.");
            RequirePanelCladding(unsupported.GeometryClass == PanelGeometryClass.UnsupportedProjection,
                "Multi-depth projection was not rejected.");
            checkpoints.Add("planar, curved/projectable, and multi-depth unsupported classification");

            IReadOnlyDictionary<string, string> requested = keySet.Cells.ToDictionary(item => item.UserTextKey, item => item.Value);
            PanelCladdingTypeIdentity planarIdentity = RequireData(signatureService.Create(planar, requested, "WT-01"), "Planar signature");
            PanelCladdingTypeIdentity planarIdentityAgain = RequireData(signatureService.Create(planar, requested, "wt01"), "Repeat signature");
            PanelCladdingTypeIdentity curvedIdentity = RequireData(signatureService.Create(curved, requested, "WT01"), "Curved signature");
            RequirePanelCladding(planarIdentity.FullDigest == planarIdentityAgain.FullDigest,
                "Identical normalized panels must produce identical signatures.");
            RequirePanelCladding(planarIdentity.FullDigest == curvedIdentity.FullDigest,
                "Equal cladding assignments must share a signature across planar and curved panels.");
            IReadOnlyDictionary<string, string> alternateGridText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
            {
                ["cw_2.03_offset_h0"] = "25",
                ["CW_2.04_OFFSET_V0"] = "75"
            };
            PanelCladdingKeySet alternateGrid = RequireData(
                keys.Parse(alternateGridText, 100d, 100d, 0.001d),
                "Parse alternate offset grid");
            PanelCladdingLayout alternateOffsetLayout = BuildSmokeLayout(
                projection,
                alternateGrid,
                PanelMesh(planar: false, duplicateDepth: false),
                1d,
                0.001d);
            PanelCladdingTypeIdentity alternateOffsetIdentity = RequireData(
                signatureService.Create(alternateOffsetLayout, requested, "WT01"),
                "Alternate-offset signature");
            RequirePanelCladding(planarIdentity.FullDigest != alternateOffsetIdentity.FullDigest,
                "Different H/V offset values must produce different region-aware signatures.");
            PanelCladdingLayout curvedMeters = ScaleLayout(curved, 0.001d, 1000d, 0.000001d);
            PanelCladdingTypeIdentity curvedMetersIdentity = RequireData(
                signatureService.Create(curvedMeters, requested, "WT01"), "Unit-independent signature");
            RequirePanelCladding(curvedIdentity.FullDigest == curvedMetersIdentity.FullDigest,
                "Equivalent millimeter and meter layouts must share a signature.");
            var changedMaterial = new Dictionary<string, string>(requested, StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.GetCellKey(0, "A")] = "GL99"
            };
            PanelCladdingTypeIdentity changedMaterialIdentity = RequireData(
                signatureService.Create(curvedMeters, changedMaterial, "WT01"),
                "Changed-material signature");
            RequirePanelCladding(planarIdentity.FullDigest != changedMaterialIdentity.FullDigest,
                "A changed cladding material must produce a different signature.");
            RequirePanelCladding(
                planarIdentity.SchemaVersion == 4 &&
                planarIdentity.StoredSignature.StartsWith("v4:sha256:", StringComparison.Ordinal) &&
                planarIdentity.TypeCode.StartsWith("WT01-2X2-", StringComparison.Ordinal) &&
                !planarIdentity.TypeCode.Contains("-CL-", StringComparison.Ordinal) &&
                planarIdentity.CanonicalPayload.StartsWith("v=4|size=", StringComparison.Ordinal) &&
                planarIdentity.CanonicalPayload.Contains("|h=", StringComparison.Ordinal) &&
                planarIdentity.CanonicalPayload.Contains("|v=", StringComparison.Ordinal) &&
                planarIdentity.CanonicalPayload.Contains("|cells=", StringComparison.Ordinal),
                "Cladding identity must use the v4 size, offset, topology, and normalized-cell payload.");
            OperationResponse<PanelCladdingTypeIdentity> unsupportedIdentity = signatureService.Create(unsupported, requested, "WT01");
            RequirePanelCladding(!unsupportedIdentity.Success, "Unsupported projection must disable type creation.");
            checkpoints.Add("deterministic v4 SHA-256 type identity with unit-normalized size, offsets, topology, and regions");

            byte[] previewPng = RequireData(renderer.RenderPng(curved, 900, 620), "Render curved preview");
            RequirePanelCladding(previewPng.Length > 8 && previewPng.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                "Preview output is not a PNG.");
            string previewPath = Path.Combine(output, "curved-panel-axonometric-preview.png");
            File.WriteAllBytes(previewPath, previewPng);
            checkpoints.Add("fixed orthographic axonometric PNG rendering");

            string workbookPath = Path.Combine(output, "panel-cladding-types.xlsx");
            CreateWorkbookFixture(workbookPath);
            PanelCladdingWorkbookUpsert upsert = new()
            {
                WorkbookPath = workbookPath,
                Layout = curved,
                Identity = curvedIdentity,
                PreviewPng = previewPng,
                AllowCreate = false
            };
            using (IPreparedPanelCladdingWorkbookUpdate prepared = RequireData(workbook.PrepareUpsert(upsert), "Prepare workbook"))
            {
                RequirePanelCladding(!prepared.Result.ReusedExistingType, "First signature unexpectedly reused a type.");
                RequireSuccess(prepared.Commit(), "Commit workbook");
            }
            ValidateWorkbookArtifact(workbookPath, curvedIdentity);
            long firstLength = new FileInfo(workbookPath).Length;
            DateTime firstWrite = File.GetLastWriteTimeUtc(workbookPath);
            using (IPreparedPanelCladdingWorkbookUpdate prepared = RequireData(workbook.PrepareUpsert(upsert), "Prepare identical workbook type"))
            {
                RequirePanelCladding(prepared.Result.ReusedExistingType, "Identical signature did not reuse its type tab.");
                RequireSuccess(prepared.Commit(), "Commit reused workbook type");
            }
            RequirePanelCladding(new FileInfo(workbookPath).Length == firstLength && File.GetLastWriteTimeUtc(workbookPath) == firstWrite,
                "Reusing an identical type should not rewrite the workbook.");
            checkpoints.Add("Open XML preservation, hidden index, visual matrix, embedded preview, and type reuse");

            using (FileStream lockStream = File.Open(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                OperationResponse<IPreparedPanelCladdingWorkbookUpdate> locked = workbook.PrepareUpsert(upsert);
                RequirePanelCladding(!locked.Success && locked.Message.Contains("WORKBOOK_LOCKED", StringComparison.Ordinal),
                    "Locked workbook must fail before Rhino mutation can begin.");
            }
            checkpoints.Add("locked-workbook preflight hard error");

            VerifyPanelCladdingSurfaceContract();
            checkpoints.Add("standalone Rhino command surface with no MCP or retired command registration");

            foreach (string checkpoint in checkpoints)
            {
                Console.WriteLine($"[OK] {checkpoint}");
            }
            Console.WriteLine(keepArtifacts
                ? $"[OK] artifacts: {output}"
                : "[OK] temporary artifacts verified and cleaned");
        }
        finally
        {
            if (!keepArtifacts && Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static PanelCladdingLayout BuildSmokeLayout(
        PanelAxonometricProjectionService projection,
        PanelCladdingKeySet keySet,
        (IReadOnlyList<PanelPoint3> Vertices, IReadOnlyList<PanelTriangle> Triangles) mesh,
        double unitScale,
        double tolerance)
    {
        var result = RequireData(projection.Build(
            mesh.Vertices,
            mesh.Triangles,
            0d,
            100d,
            0d,
            100d,
            keySet.HorizontalOffsets,
            keySet.VerticalOffsets,
            keySet.Cells,
            tolerance), "Build preview projection");
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DocumentPath = "C:/smoke/panel.3dm",
            LayerFullPath = "WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "smoke",
            GeometryClass = result.Classification,
            GeometryDiagnostic = result.Diagnostic,
            Width = 100d,
            Height = 100d,
            ModelTolerance = tolerance,
            ModelUnitScaleToMillimeters = unitScale,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Preview = result.Preview
        };
    }

    private static (IReadOnlyList<PanelPoint3> Vertices, IReadOnlyList<PanelTriangle> Triangles) PanelMesh(
        bool planar,
        bool duplicateDepth)
    {
        double middle = planar ? 0d : 8d;
        var vertices = new List<PanelPoint3>
        {
            new(0, 0, 0), new(50, 0, middle), new(100, 0, 0),
            new(0, 100, 0), new(50, 100, middle), new(100, 100, 0)
        };
        var triangles = new List<PanelTriangle>
        {
            new(0, 1, 4), new(0, 4, 3), new(1, 2, 5), new(1, 5, 4)
        };
        if (duplicateDepth)
        {
            int start = vertices.Count;
            vertices.AddRange(vertices.Take(6).Select(point => new PanelPoint3(point.X, point.Y, point.Z + 20d)));
            triangles.AddRange(new[]
            {
                new PanelTriangle(start, start + 1, start + 4),
                new PanelTriangle(start, start + 4, start + 3),
                new PanelTriangle(start + 1, start + 2, start + 5),
                new PanelTriangle(start + 1, start + 5, start + 4)
            });
        }
        return (vertices, triangles);
    }

    private static PanelCladdingLayout ScaleLayout(
        PanelCladdingLayout source,
        double geometryScale,
        double unitScale,
        double modelTolerance)
    {
        return new PanelCladdingLayout
        {
            ObjectId = source.ObjectId,
            DocumentPath = source.DocumentPath,
            LayerFullPath = source.LayerFullPath,
            SystemCode = source.SystemCode,
            GeometryFingerprint = source.GeometryFingerprint,
            GeometryClass = source.GeometryClass,
            GeometryDiagnostic = source.GeometryDiagnostic,
            Width = source.Width * geometryScale,
            Height = source.Height * geometryScale,
            ModelTolerance = modelTolerance,
            ModelUnitScaleToMillimeters = unitScale,
            HorizontalOffsets = source.HorizontalOffsets.Select(value => value * geometryScale).ToArray(),
            VerticalOffsets = source.VerticalOffsets.Select(value => value * geometryScale).ToArray(),
            Cells = source.Cells,
            Preview = new PanelPreviewGeometry
            {
                Vertices = source.Preview.Vertices.Select(point => new PanelPoint3(point.X * geometryScale, point.Y * geometryScale, point.Z * geometryScale)).ToArray(),
                Triangles = source.Preview.Triangles,
                GridPolylines = source.Preview.GridPolylines.Select(line =>
                    (IReadOnlyList<PanelPoint3>)line.Select(point => new PanelPoint3(point.X * geometryScale, point.Y * geometryScale, point.Z * geometryScale)).ToArray()).ToArray(),
                Cells = source.Preview.Cells.Select(cell => new PanelPreviewCell
                {
                    UserTextKey = cell.UserTextKey,
                    Center = new PanelPoint3(cell.Center.X * geometryScale, cell.Center.Y * geometryScale, cell.Center.Z * geometryScale),
                    Boundary = cell.Boundary.Select(point => new PanelPoint3(point.X * geometryScale, point.Y * geometryScale, point.Z * geometryScale)).ToArray()
                }).ToArray(),
                DepthSamples = source.Preview.DepthSamples.Select(value => value * geometryScale).ToArray()
            }
        };
    }

    private static void CreateWorkbookFixture(string path)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new S.Workbook();
        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new S.Worksheet(new S.SheetData(
            new S.Row(new S.Cell
            {
                CellReference = "A1",
                DataType = S.CellValues.InlineString,
                InlineString = new S.InlineString(new S.Text("Existing project notes"))
            }) { RowIndex = 1U }));
        var sheets = workbookPart.Workbook.AppendChild(new S.Sheets());
        sheets.Append(new S.Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Read Me"
        });
        workbookPart.Workbook.Save();
    }

    private static void ValidateWorkbookArtifact(string path, PanelCladdingTypeIdentity identity)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
        WorkbookPart workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("WorkbookPart missing.");
        S.Workbook workbook = workbookPart.Workbook ?? throw new InvalidOperationException("Workbook missing.");
        S.Sheets sheets = workbook.GetFirstChild<S.Sheets>() ?? throw new InvalidOperationException("Sheets missing.");
        S.Sheet readMe = sheets.Elements<S.Sheet>().Single(item => item.Name?.Value == "Read Me");
        RequirePanelCladding(readMe is not null, "Existing unrelated worksheet was not preserved.");
        S.Sheet index = sheets.Elements<S.Sheet>().Single(item => item.Name?.Value == "_CLADDING_INDEX");
        RequirePanelCladding(index.State?.Value == S.SheetStateValues.Hidden, "Cladding index must be hidden.");
        S.Sheet typeSheet = sheets.Elements<S.Sheet>().Single(item => item.Name?.Value == identity.TypeCode);
        WorksheetPart typePart = (WorksheetPart)workbookPart.GetPartById(typeSheet.Id!);
        RequirePanelCladding(typePart.DrawingsPart is not null && typePart.DrawingsPart.ImageParts.Any(),
            "Type sheet must contain the embedded axonometric preview.");
        S.Worksheet worksheet = typePart.Worksheet ?? throw new InvalidOperationException("Type worksheet missing.");
        S.SheetData data = worksheet.GetFirstChild<S.SheetData>() ?? throw new InvalidOperationException("Type sheet data missing.");
        RequirePanelCladding(ReadInline(data, "B7") == "TER01" && ReadInline(data, "C7") == "GL01",
            "Top visual worksheet row must represent logical row B.");
        RequirePanelCladding(ReadInline(data, "B8") == "GL02" && ReadInline(data, "C8") == "STN02",
            "Bottom visual worksheet row must represent logical row A.");
    }

    private static string ReadInline(S.SheetData data, string reference)
    {
        S.Cell cell = data.Descendants<S.Cell>().Single(item => item.CellReference?.Value == reference);
        return cell.InlineString?.InnerText ?? cell.CellValue?.Text ?? string.Empty;
    }

    private static void VerifyPanelCladdingSurfaceContract()
    {
        System.Reflection.Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        RequirePanelCladding(
            assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorCommand") is not null,
            "Production PanelCladdingEditor command is missing.");
        RequirePanelCladding(
            assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorSmokeCommand") is null,
            "The retired Rhino-side smoke command must not be exposed to users.");
        RequirePanelCladding(
            assembly.GetType("PanelCladdingEditor.PanelCladdingEditorPlugin")?.GUID ==
                Guid.Parse("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35"),
            "Standalone plug-in must use the clean product GUID.");

        Type editorCommand = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorCommand")!;
        Guid editorCommandId = editorCommand.GUID;
        RequirePanelCladding(editorCommandId != Guid.Empty,
            "The editor Rhino command must have an explicit non-empty GUID.");

        string[] forbiddenReferences = { "MCP_Rhino", "ModelContextProtocol", "Microsoft.Extensions.Hosting" };
        string[] references = assembly.GetReferencedAssemblies().Select(item => item.Name ?? string.Empty).ToArray();
        RequirePanelCladding(!references.Any(reference => forbiddenReferences.Any(forbidden =>
                reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase))),
            "Standalone assembly contains an MCP_Rhino or MCP SDK reference.");

        Type[] panelTypes =
        {
            typeof(PanelCladdingKeyService),
            typeof(PanelCladdingTypeSignatureService),
            typeof(PanelAxonometricProjectionService),
            typeof(PanelCladdingSaveService),
            typeof(OpenXmlPanelCladdingWorkbookRepository),
            typeof(PanelPreviewRenderer)
        };
        bool exposesMcpTool = panelTypes.SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetCustomAttributesData())
            .Any(attribute => attribute.AttributeType.Name.Contains("McpServerTool", StringComparison.Ordinal));
        RequirePanelCladding(!exposesMcpTool, "Panel cladding editor must not expose an MCP tool surface.");
    }

    private static T RequireData<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void RequireSuccess(OperationResponse response, string operation)
    {
        if (!response.Success)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
    }

    private static void RequirePanelCladding(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

