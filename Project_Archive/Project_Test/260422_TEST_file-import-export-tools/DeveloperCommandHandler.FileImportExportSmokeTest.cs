extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Tools.File.Export;
using MCP_Rhino.Server.Tools.File.Reference;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterFileImportExportHandlers()
    {
        _extensionHandlers["file-import-export-smoke-test"] = HandleFileImportExportSmokeTest;
    }

    private bool HandleFileImportExportSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- file-import-export-smoke-test <3dm-file-path>");
            Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string filePath = Path.GetFullPath(args[1]);
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Smoke test source file was not found: {filePath}");
                Environment.ExitCode = 1;
                return true;
            }

            if (MCP_Rhino.Server.Infrastructure.Plugin.McpRhinoPlugin.Instance is null)
            {
                RunCliFallbackFileImportExportSmoke(filePath);
            }
            else
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new InvalidOperationException("Live file import/export smoke requires Windows.");
                }

                RunLiveFileImportExportSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"File import/export smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackFileImportExportSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        string outputRoot = PrepareOutputRoot();

        var exportDwgTool = new ExportToDwgTool(_fileExportService);
        var exportDxfTool = new ExportToDxfTool(_fileExportService);
        var exportIfcTool = new ExportToIfcTool(_fileExportService);
        var exportStlTool = new ExportToStlTool(_fileExportService);
        var exportImageTool = new ExportToImageTool(_fileExportService);
        var exportPdfTool = new ExportToPdfTool(_fileExportService);
        var listWorksessionTool = new ListWorksessionAttachmentsTool(_externalReferenceService);
        var updateLinkedBlockTool = new UpdateLinkedBlockTool(_externalReferenceService);

        RequireFileImportExportFailureWithMessage(
            exportDwgTool.ExportToDwg(filePath, Path.Combine(outputRoot, "cli-fallback.dwg")),
            "LIVE_RHINO_REQUIRED",
            "ExportToDwg should require live Rhino.");
        checkpoints.Add("ExportToDwg rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            exportDxfTool.ExportToDxf(filePath, Path.Combine(outputRoot, "cli-fallback.dxf")),
            "LIVE_RHINO_REQUIRED",
            "ExportToDxf should require live Rhino.");
        checkpoints.Add("ExportToDxf rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            exportIfcTool.ExportToIfc(filePath, Path.Combine(outputRoot, "cli-fallback.ifc")),
            "LIVE_RHINO_REQUIRED",
            "ExportToIfc should require live Rhino.");
        checkpoints.Add("ExportToIfc rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            exportStlTool.ExportToStl(filePath, Path.Combine(outputRoot, "cli-fallback.stl")),
            "LIVE_RHINO_REQUIRED",
            "ExportToStl should require live Rhino.");
        checkpoints.Add("ExportToStl rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            exportImageTool.ExportToImage(filePath, Path.Combine(outputRoot, "cli-fallback.png")),
            "LIVE_RHINO_REQUIRED",
            "ExportToImage should require live Rhino.");
        checkpoints.Add("ExportToImage rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            exportPdfTool.ExportToPdf(filePath, Path.Combine(outputRoot, "cli-fallback.pdf")),
            "LIVE_RHINO_REQUIRED",
            "ExportToPdf should require live Rhino.");
        checkpoints.Add("ExportToPdf rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            listWorksessionTool.ListWorksessionAttachments(filePath),
            "LIVE_RHINO_REQUIRED",
            "ListWorksessionAttachments should require live Rhino.");
        checkpoints.Add("ListWorksessionAttachments rejected in CLI fallback");

        RequireFileImportExportFailureWithMessage(
            updateLinkedBlockTool.UpdateLinkedBlock(filePath, new List<string> { "__missing__" }),
            "LIVE_RHINO_REQUIRED",
            "UpdateLinkedBlock should require live Rhino.");
        checkpoints.Add("UpdateLinkedBlock rejected in CLI fallback");

        Console.WriteLine("File import/export smoke test completed successfully (CLI fallback mode).");
        Console.WriteLine($"Source: {filePath}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RunLiveFileImportExportSmoke(string filePath)
    {
        var checkpoints = new List<string>();
        string outputRoot = PrepareOutputRoot();
        CleanupOutputRoot(outputRoot);

        FileImportExportDocumentSnapshot before = CaptureFileImportExportSnapshot(filePath);
        Guid firstObjectId = GetFirstLiveObjectId(filePath);

        var exportDwgTool = new ExportToDwgTool(_fileExportService);
        var exportDxfTool = new ExportToDxfTool(_fileExportService);
        var exportIfcTool = new ExportToIfcTool(_fileExportService);
        var exportStlTool = new ExportToStlTool(_fileExportService);
        var exportImageTool = new ExportToImageTool(_fileExportService);
        var exportPdfTool = new ExportToPdfTool(_fileExportService);
        var listWorksessionTool = new ListWorksessionAttachmentsTool(_externalReferenceService);
        var updateLinkedBlockTool = new UpdateLinkedBlockTool(_externalReferenceService);

        string dwgPath = Path.Combine(outputRoot, "fixture-export.dwg");
        string dxfPath = Path.Combine(outputRoot, "fixture-export.dxf");
        string ifcPath = Path.Combine(outputRoot, "fixture-export.ifc");
        string stlPath = Path.Combine(outputRoot, "fixture-selected.stl");
        string pngPath = Path.Combine(outputRoot, "fixture-view.png");
        string pdfPath = Path.Combine(outputRoot, "fixture-view.pdf");

        FileExportResponse dwg = RequireFileImportExportSuccess(
            exportDwgTool.ExportToDwg(filePath, dwgPath),
            "ExportToDwg");
        RequireFileExistsWithBytes(dwg.OutputPath, "ExportToDwg should create a non-empty file.");
        checkpoints.Add("ExportToDwg ok");

        FileExportResponse dxf = RequireFileImportExportSuccess(
            exportDxfTool.ExportToDxf(filePath, dxfPath),
            "ExportToDxf");
        RequireFileExistsWithBytes(dxf.OutputPath, "ExportToDxf should create a non-empty file.");
        checkpoints.Add("ExportToDxf ok");

        FileExportResponse ifc = RequireFileImportExportSuccess(
            exportIfcTool.ExportToIfc(filePath, ifcPath),
            "ExportToIfc");
        RequireFileExistsWithBytes(ifc.OutputPath, "ExportToIfc should create a non-empty file.");
        checkpoints.Add("ExportToIfc ok");

        FileExportResponse stl = RequireFileImportExportSuccess(
            exportStlTool.ExportToStl(
                filePath,
                stlPath,
                new List<Guid> { firstObjectId },
                overwriteExisting: true,
                formatOptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Binary"] = "true"
                }),
            "ExportToStl");
        RequireFileExistsWithBytes(stl.OutputPath, "ExportToStl should create a non-empty file.");
        RequireFileImportExport(
            stl.Warnings.Any(warning => warning.Code == "FORMAT_OPTIONS_NOT_APPLIED"),
            "ExportToStl should surface the current formatOptions limitation as a warning.");
        checkpoints.Add("ExportToStl ok");

        FileExportResponse image = RequireFileImportExportSuccess(
            exportImageTool.ExportToImage(
                filePath,
                pngPath,
                imageSizePx: new ImageSizePxRequest { Width = 1280, Height = 720 },
                dotsPerInch: 144d,
                backgroundTransparent: true),
            "ExportToImage");
        RequireFileExistsWithBytes(image.OutputPath, "ExportToImage should create a non-empty file.");
        using (var imageFile = Image.FromFile(image.OutputPath))
        {
            RequireFileImportExport(imageFile.Width == 1280 && imageFile.Height == 720, "ExportToImage should respect the requested pixel size.");
        }
        checkpoints.Add("ExportToImage ok");

        FileExportResponse pdf = RequireFileImportExportSuccess(
            exportPdfTool.ExportToPdf(
                filePath,
                pdfPath,
                pageSizeMm: new PageSizeMmRequest { WidthMm = 420d, HeightMm = 297d },
                dotsPerInch: 300d),
            "ExportToPdf");
        RequireFileExistsWithBytes(pdf.OutputPath, "ExportToPdf should create a non-empty file.");
        checkpoints.Add("ExportToPdf ok");

        WorksessionAttachmentResponse worksession = RequireFileImportExportSuccess(
            listWorksessionTool.ListWorksessionAttachments(filePath),
            "ListWorksessionAttachments");
        RequireFileImportExport(worksession.AttachedFiles.Count == worksession.Results.Count, "ListWorksessionAttachments response shape should stay aligned.");
        checkpoints.Add("ListWorksessionAttachments ok");

        LinkedBlockMutationResponse missingLinkedBlock = RequireFileImportExportSuccess(
            updateLinkedBlockTool.UpdateLinkedBlock(filePath, new List<string> { "__missing_linked_block__" }),
            "UpdateLinkedBlock(missing)");
        RequireFileImportExport(missingLinkedBlock.SucceededCount == 0 && missingLinkedBlock.FailedCount == 1, "UpdateLinkedBlock missing-definition probe should fail per entry.");
        RequireFileImportExport(
            string.Equals(missingLinkedBlock.Results[0].Message, "LINKED_BLOCK_DEFINITION_NOT_FOUND", StringComparison.Ordinal),
            "UpdateLinkedBlock missing-definition probe should return LINKED_BLOCK_DEFINITION_NOT_FOUND.");
        checkpoints.Add("UpdateLinkedBlock missing-definition path ok");

        RequireFileImportExportFailureWithMessage(
            exportDwgTool.ExportToDwg(filePath, Path.Combine(outputRoot, "missing-parent", "blocked.dwg")),
            "EXPORT_OUTPUT_PARENT_NOT_FOUND",
            "Export should fail when the output parent directory does not exist.");
        checkpoints.Add("Missing-parent guard ok");

        RequireFileImportExportFailureWithMessage(
            exportDwgTool.ExportToDwg(filePath, dwgPath, overwriteExisting: false),
            "EXPORT_OUTPUT_OVERWRITE_BLOCKED",
            "Export should fail when overwriteExisting is false and the target file already exists.");
        checkpoints.Add("Overwrite guard ok");

        RequireFileImportExportFailureWithMessage(
            exportImageTool.ExportToImage(filePath, Path.Combine(outputRoot, "missing-view.png"), viewName: "__missing_view__"),
            "EXPORT_VIEW_NOT_FOUND",
            "ExportToImage should fail when the requested view does not exist.");
        checkpoints.Add("Missing-view guard ok");

        FileImportExportDocumentSnapshot after = CaptureFileImportExportSnapshot(filePath);
        RequireFileImportExport(after.ObjectCount == before.ObjectCount, "Smoke should not change object count.");
        RequireFileImportExport(after.LayerCount == before.LayerCount, "Smoke should not change layer count.");
        RequireFileImportExport(after.DocumentStringCount == before.DocumentStringCount, "Smoke should not change document user strings.");
        RequireFileImportExport(after.ObjectUserTextKeyCount == before.ObjectUserTextKeyCount, "Smoke should not change object user text.");
        RequireFileImportExport(after.DocumentPath == before.DocumentPath, "Smoke should not change document path.");
        RequireFileImportExport(after.DocumentName == before.DocumentName, "Smoke should not change document title.");
        RequireFileImportExport(after.NextUndoRecordSerialNumber == before.NextUndoRecordSerialNumber, "Smoke should not create an undo entry when no linked block was updated.");
        RequireFileImportExport(after.CurrentUndoRecordSerialNumber == before.CurrentUndoRecordSerialNumber, "Smoke should not change current undo serial when no linked block was updated.");
        checkpoints.Add("Read-only state preserved");

        Console.WriteLine("File import/export smoke test completed successfully (live Rhino mode).");
        Console.WriteLine($"Active file: {filePath}");
        Console.WriteLine($"Output root: {outputRoot}");
        foreach (string checkpoint in checkpoints)
        {
            Console.WriteLine($"- {checkpoint}");
        }
    }

    private FileImportExportDocumentSnapshot CaptureFileImportExportSnapshot(string filePath)
    {
        return RequireFileImportExportSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                int objectUserTextKeyCount = 0;
                foreach (RhinoObject rhinoObject in document.Objects)
                {
                    if (rhinoObject.IsDeleted)
                    {
                        continue;
                    }

                    var userStrings = rhinoObject.Attributes.GetUserStrings();
                    if (userStrings is not null)
                    {
                        objectUserTextKeyCount += userStrings.Count;
                    }
                }

                return OperationResponse<FileImportExportDocumentSnapshot>.Ok(new FileImportExportDocumentSnapshot
                {
                    ObjectCount = document.Objects.Count,
                    LayerCount = Enumerable.Range(0, document.Layers.Count).Count(index => !document.Layers[index].IsDeleted),
                    DocumentStringCount = document.Strings.Count,
                    ObjectUserTextKeyCount = objectUserTextKeyCount,
                    NextUndoRecordSerialNumber = document.NextUndoRecordSerialNumber,
                    CurrentUndoRecordSerialNumber = document.CurrentUndoRecordSerialNumber,
                    DocumentPath = document.Path,
                    DocumentName = document.Name
                });
            }),
            "CaptureFileImportExportSnapshot");
    }

    private Guid GetFirstLiveObjectId(string filePath)
    {
        return RequireFileImportExportSuccess(
            _liveRhinoDocumentAccessor.Execute(filePath, document =>
            {
                foreach (RhinoObject rhinoObject in document.Objects)
                {
                    if (!rhinoObject.IsDeleted)
                    {
                        return OperationResponse<Guid>.Ok(rhinoObject.Id);
                    }
                }

                return OperationResponse<Guid>.Fail("The live fixture does not contain any objects.");
            }),
            "GetFirstLiveObjectId");
    }

    private static string PrepareOutputRoot()
    {
        string outputRoot = Path.GetFullPath(Path.Combine("Project_Test", "260422_TEST_file-import-export-tools", "output"));
        Directory.CreateDirectory(outputRoot);
        return outputRoot;
    }

    private static void CleanupOutputRoot(string outputRoot)
    {
        if (!Directory.Exists(outputRoot))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(outputRoot))
        {
            File.Delete(filePath);
        }
    }

    private static void RequireFileExistsWithBytes(string filePath, string message)
    {
        RequireFileImportExport(File.Exists(filePath), message);
        RequireFileImportExport(new FileInfo(filePath).Length > 0L, message);
    }

    private static T RequireFileImportExportSuccess<T>(OperationResponse<T> response, string operationName)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operationName} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireFileImportExportFailureWithMessage<T>(OperationResponse<T> response, string expectedMessage, string message)
    {
        if (response.Success)
        {
            throw new InvalidOperationException($"{message} Expected failure but got success.");
        }

        if (!string.Equals(response.Message, expectedMessage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected [{expectedMessage}], actual [{response.Message}].");
        }
    }

    private static void RequireFileImportExport(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FileImportExportDocumentSnapshot
    {
        public int ObjectCount { get; set; }
        public int LayerCount { get; set; }
        public int DocumentStringCount { get; set; }
        public int ObjectUserTextKeyCount { get; set; }
        public uint NextUndoRecordSerialNumber { get; set; }
        public uint CurrentUndoRecordSerialNumber { get; set; }
        public string DocumentPath { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
    }
}
