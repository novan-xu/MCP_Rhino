extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;
using FileReference = rhinocommon::Rhino.FileIO.FileReference;
using InstanceDefinition = rhinocommon::Rhino.DocObjects.InstanceDefinition;
using InstanceDefinitionUpdateType = rhinocommon::Rhino.DocObjects.InstanceDefinitionUpdateType;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string FileExportReliabilitySlug = "file-export-reliability-smoke-test";

    partial void RegisterFileExportReliabilityHandlers()
    {
        _extensionHandlers[FileExportReliabilitySlug] = HandleFileExportReliabilitySmokeTest;
    }

    private bool HandleFileExportReliabilitySmokeTest(string[] args)
    {
        try
        {
            string filePath = args.Length > 1
                ? Path.GetFullPath(args[1])
                : "C:/mcp-rhino/file-export-reliability-smoke.3dm";

            if (McpRhinoPlugin.Instance is null)
            {
                RunFileExportReliabilityCliFallbackSmoke(filePath);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    throw new InvalidOperationException("Live file export reliability smoke requires a saved active document path.");
                }

                RunFileExportReliabilityLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"File export reliability smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunFileExportReliabilityCliFallbackSmoke(string filePath)
    {
        string outputRoot = PrepareFileExportReliabilityOutputRoot();

        RequireFileExportReliabilityFailure(
            _viewportCaptureService.Capture(new CaptureViewportImageRequest
            {
                FilePath = filePath,
                ImageSizePx = new ImageSizePxRequest { Width = 64, Height = 64 }
            }),
            "LIVE_RHINO_REQUIRED",
            "capture_viewport_image should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToImage(new ExportToImageRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.png"),
                ImageSizePx = new ImageSizePxRequest { Width = 64, Height = 64 }
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_image should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToPdf(new ExportToPdfRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.pdf"),
                PageSizeMm = new PageSizeMmRequest { WidthMm = 100d, HeightMm = 100d }
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_pdf should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToDwg(new ExportToDwgRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.dwg")
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_dwg should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToDxf(new ExportToDxfRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.dxf")
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_dxf should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToStl(new ExportToStlRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.stl")
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_stl should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _fileExportService.ExportToIfc(new ExportToIfcRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "cli-fallback.ifc")
            }),
            "LIVE_RHINO_REQUIRED",
            "export_to_ifc should require live Rhino in CLI fallback mode.");

        RequireFileExportReliabilityFailure(
            _drawingExportService.ExportDrawingPackage(new ExportDrawingPackageRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                ExportPdf = true,
                ExportJpg = true,
                ViewNames = new List<string> { "MCP_Elevation_Front" }
            }),
            "LIVE_RHINO_REQUIRED",
            "export_drawing_package should require live Rhino in CLI fallback mode.");

        Console.WriteLine("[OK] file-export-reliability CLI fallback smoke completed.");
    }

    private void RunFileExportReliabilityLiveSmoke(string filePath)
    {
        string outputRoot = PrepareFileExportReliabilityOutputRoot();
        CleanupFileExportReliabilityOutput(outputRoot);

        string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string smokeLayer = $"MCP::FILE_EXPORT_RELIABILITY_SMOKE_{suffix}";
        string blockName = $"MCP_FILE_EXPORT_RELIABILITY_BLOCK_{suffix}";

        RequireFileExportReliabilitySuccess(_layerManagementService.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = new List<LayerCreationEntryRequest>
            {
                new() { FullPath = smokeLayer }
            }
        }), "Create file export smoke layer");

        ArchitecturalCreationResponse boxes = RequireFileExportReliabilitySuccess(
            _architecturalPrimitiveCreationSkill.CreateBoxes(new CreateBoxesRequest
            {
                FilePath = filePath,
                AutoCreateLayers = false,
                Common = new GeometryCreationCommonOptions
                {
                    LayerFullPath = smokeLayer,
                    Name = "file export reliability smoke box",
                    UserText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mcp.capability"] = "file-export-reliability"
                    }
                },
                Metadata = new ArchitecturalMetadataRequest
                {
                    Category = "Smoke",
                    SystemName = "FileExportReliability",
                    SourceTag = suffix
                },
                Items = new List<BoxItemRequest>
                {
                    new()
                    {
                        OriginX = 0d,
                        OriginY = 0d,
                        OriginZ = 0d,
                        SizeX = 4d,
                        SizeY = 3d,
                        SizeZ = 2d,
                        Representation = ArchitecturalRepresentationKind.Brep,
                        Name = "file_export_reliability_box"
                    }
                }
            }),
            "Create file export smoke box");

        List<Guid> selectedObjectIds = boxes.CreatedObjects.Select(item => item.ObjectId).Where(id => id != Guid.Empty).ToList();
        RequireFileExportReliability(selectedObjectIds.Count == 1, $"Expected one smoke object id, got {selectedObjectIds.Count}.");

        ViewportCaptureResponse capture = RequireFileExportReliabilitySuccess(
            _viewportCaptureService.Capture(new CaptureViewportImageRequest
            {
                FilePath = filePath,
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 200 },
                BackgroundTransparent = false
            }),
            "Capture viewport image");
        RequireFileExportReliability(capture.ByteCount > 0 && !string.IsNullOrWhiteSpace(capture.DataBase64), "Viewport capture should return non-empty base64 PNG data.");

        FileExportResponse png = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToImage(new ExportToImageRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.png"),
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 200 },
                DotsPerInch = 96d
            }),
            "Export PNG");
        RequireFileExportReliabilityOutput(png.OutputPath, "PNG export should create a non-empty file.");

        FileExportResponse jpg = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToImage(new ExportToImageRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.jpg"),
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 200 },
                DotsPerInch = 96d
            }),
            "Export JPG");
        RequireFileExportReliabilityOutput(jpg.OutputPath, "JPG export should create a non-empty file.");

        FileExportResponse pdf = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToPdf(new ExportToPdfRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.pdf"),
                PageSizeMm = new PageSizeMmRequest { WidthMm = 100d, HeightMm = 100d },
                DotsPerInch = 96d
            }),
            "Export PDF");
        RequireFileExportReliabilityOutput(pdf.OutputPath, "PDF export should create a non-empty file.");

        FileExportResponse dwg = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToDwg(new ExportToDwgRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.dwg"),
                SelectedObjectIds = selectedObjectIds
            }),
            "Export DWG");
        RequireFileExportReliabilityOutput(dwg.OutputPath, "DWG export should create a non-empty file.");

        FileExportResponse dxf = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToDxf(new ExportToDxfRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.dxf"),
                SelectedObjectIds = selectedObjectIds
            }),
            "Export DXF");
        RequireFileExportReliabilityOutput(dxf.OutputPath, "DXF export should create a non-empty file.");

        FileExportResponse stl = RequireFileExportReliabilitySuccess(
            _fileExportService.ExportToStl(new ExportToStlRequest
            {
                FilePath = filePath,
                OutputPath = Path.Combine(outputRoot, "file-export-reliability.stl"),
                SelectedObjectIds = selectedObjectIds,
                FormatOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["BinaryFile"] = "true"
                }
            }),
            "Export STL");
        RequireFileExportReliabilityOutput(stl.OutputPath, "STL export should create a non-empty file.");

        OperationResponse<FileExportResponse> ifcResponse = _fileExportService.ExportToIfc(new ExportToIfcRequest
        {
            FilePath = filePath,
            OutputPath = Path.Combine(outputRoot, "file-export-reliability.ifc"),
            SelectedObjectIds = selectedObjectIds
        });
        if (ifcResponse.Success && ifcResponse.Data is not null)
        {
            RequireFileExportReliabilityOutput(ifcResponse.Data.OutputPath, "IFC export should create a non-empty file when host support is available.");
        }
        else
        {
            RequireFileExportReliability(
                ifcResponse.Message.Contains("IFC_EXPORT_UNAVAILABLE", StringComparison.OrdinalIgnoreCase),
                $"IFC export should return a precise host-support message when unavailable. Actual: {ifcResponse.Message}");
        }

        DrawingExportPackageResponse package = RequireFileExportReliabilitySuccess(
            _drawingExportService.ExportDrawingPackage(new ExportDrawingPackageRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                ConfirmedLayerFullPaths = new List<string> { smokeLayer },
                ExportPdf = true,
                ExportJpg = true,
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 200 },
                PageSizeMm = new PageSizeMmRequest { WidthMm = 100d, HeightMm = 100d },
                DotsPerInch = 96d,
                ViewNames = new List<string> { "MCP_Elevation_Front" },
                TemporaryObjectColor = new ObjectColorRequest { R = 24, G = 24, B = 24 }
            }),
            "Export drawing package");
        RequireFileExportReliability(package.RestoreSucceeded, "Drawing package should restore drawing state.");
        RequireFileExportReliability(package.ExportedFiles.Count == 2, "Drawing package should produce one PDF and one JPG.");
        foreach (DrawingExportItemResponse item in package.ExportedFiles)
        {
            RequireFileExportReliabilityOutput(item.OutputPath, $"Drawing package file should be non-empty: {item.OutputPath}");
        }

        BlockMutationApplyResponse block = RequireFileExportReliabilitySuccess(
            _blockLifecycleSkill.ApplyCreate(new ApplyCreateBlockDefinitionsRequest
            {
                FilePath = filePath,
                Items = new List<BlockDefinitionSourceItemRequest>
                {
                    new()
                    {
                        Name = blockName,
                        Description = "File export reliability linked-block smoke fixture",
                        SourceObjectIds = selectedObjectIds,
                        BasePoint = new BlockPointRequest { X = 0d, Y = 0d, Z = 0d },
                        SourceObjectPolicy = BlockSourceObjectPolicy.KeepVisible,
                        DuplicateDefinitionPolicy = BlockDuplicateDefinitionPolicy.VersionedName
                    }
                }
            }),
            "Create local block fixture");
        RequireFileExportReliability(block.FailedCount == 0, "Local block fixture creation should succeed.");

        LinkedBlockMutationResponse localBlockUpdate = RequireFileExportReliabilitySuccess(
            _externalReferenceService.UpdateLinkedBlock(new UpdateLinkedBlockRequest
            {
                FilePath = filePath,
                DefinitionNames = new List<string> { blockName }
            }),
            "Update local block negative fixture");
        RequireFileExportReliability(localBlockUpdate.FailedCount == 1, "Local block update should fail per entry.");
        RequireFileExportReliability(
            localBlockUpdate.Results[0].Message.Contains("not a linked block", StringComparison.OrdinalIgnoreCase),
            "Local block negative path should report that the definition is not linked.");

        PromoteBlockDefinitionToLinkedFixture(filePath, blockName);
        LinkedBlockMutationResponse linkedBlockUpdate = RequireFileExportReliabilitySuccess(
            _externalReferenceService.UpdateLinkedBlock(new UpdateLinkedBlockRequest
            {
                FilePath = filePath,
                DefinitionNames = new List<string> { blockName }
            }),
            "Update linked block fixture");
        RequireFileExportReliability(linkedBlockUpdate.Results.Count == 1, "Linked block update should return one result.");
        RequireFileExportReliability(
            !linkedBlockUpdate.Results[0].Message.Contains("not a linked block", StringComparison.OrdinalIgnoreCase),
            $"Linked block fixture should reach the linked-block refresh path. Actual: {linkedBlockUpdate.Results[0].Message}");

        Console.WriteLine("[OK] file-export-reliability live smoke completed.");
        Console.WriteLine($"[OK] outputRoot={outputRoot}");
        Console.WriteLine($"[OK] captureBytes={capture.ByteCount}; exportedFiles={7 + package.ExportedFiles.Count}; ifcSuccess={ifcResponse.Success}");
    }

    private void PromoteBlockDefinitionToLinkedFixture(string filePath, string definitionName)
    {
        OperationResponse<string> promoted = _liveRhinoDocumentAccessor.ExecuteWithUndo(
            filePath,
            "MCP: FileExportReliabilityLinkedBlockFixture",
            document =>
            {
                InstanceDefinition? definition = document.InstanceDefinitions.Find(definitionName);
                if (definition is null)
                {
                    return OperationResponse<(bool Mutated, string Result)>.Fail("LINKED_BLOCK_FIXTURE_DEFINITION_NOT_FOUND");
                }

                FileReference reference = FileReference.CreateFromFullPath(filePath);
                bool modified = document.InstanceDefinitions.ModifySourceArchive(
                    definition.Index,
                    reference,
                    InstanceDefinitionUpdateType.Linked,
                    true);
                return modified
                    ? OperationResponse<(bool Mutated, string Result)>.Ok((true, filePath), "Linked block fixture prepared.")
                    : OperationResponse<(bool Mutated, string Result)>.Fail("LINKED_BLOCK_FIXTURE_SOURCE_ARCHIVE_NOT_MODIFIED");
            });

        if (!promoted.Success)
        {
            throw new InvalidOperationException($"Could not prepare linked block fixture: {promoted.Message}");
        }
    }

    private static string PrepareFileExportReliabilityOutputRoot()
    {
        string outputRoot = ResolveValidationDirectory(FileExportReliabilitySlug);
        Directory.CreateDirectory(outputRoot);
        return outputRoot;
    }

    private static void CleanupFileExportReliabilityOutput(string outputRoot)
    {
        foreach (string filePath in Directory.EnumerateFiles(outputRoot, "file-export-reliability*.*"))
        {
            File.Delete(filePath);
        }

        foreach (string filePath in Directory.EnumerateFiles(outputRoot, "MCP_*.*"))
        {
            File.Delete(filePath);
        }
    }

    private static void RequireFileExportReliabilityOutput(string outputPath, string message)
    {
        RequireFileExportReliability(File.Exists(outputPath), message);
        RequireFileExportReliability(new FileInfo(outputPath).Length > 0L, message);
    }

    private static T RequireFileExportReliabilitySuccess<T>(OperationResponse<T> response, string label)
        where T : class
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequireFileExportReliabilityFailure<T>(
        OperationResponse<T> response,
        string expectedMessageFragment,
        string message)
    {
        if (response.Success || !response.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{message} Actual: Success={response.Success}, Message={response.Message}");
        }
    }

    private static void RequireFileExportReliability(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
