extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Tools.Drawing;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterDrawingExportHandlers()
    {
        _extensionHandlers["drawing-export-smoke-test"] = HandleDrawingExportSmokeTest;
    }

    private bool HandleDrawingExportSmokeTest(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: dotnet run --project src/MCP_Rhino.Server -- drawing-export-smoke-test <3dm-file-path>");
            Environment.ExitCode = 1;
            return true;
        }

        try
        {
            string filePath = Path.GetFullPath(args[1]);
            if (McpRhinoPlugin.Instance is null)
            {
                RunCliFallbackDrawingExportSmoke(filePath);
            }
            else
            {
                RunLiveDrawingExportSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Drawing export smoke test failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunCliFallbackDrawingExportSmoke(string filePath)
    {
        string outputRoot = ResolveValidationDirectory("drawing-export-smoke-test");
        var setupTool = new SetupDrawingViewsTool(_drawingExportService);
        var captureTool = new CaptureDrawingExportStateTool(_drawingExportService);
        var styleTool = new ApplyDrawingExportStyleTool(_drawingExportService);
        var backgroundTool = new SetDrawingExportBackgroundTool(_drawingExportService);
        var restoreTool = new RestoreDrawingExportStateTool(_drawingExportService);
        var packageTool = new ExportDrawingPackageTool(_drawingExportService);

        RequireDrawingExportFailure(setupTool.SetupDrawingViews(filePath), "LIVE_RHINO_REQUIRED", "SetupDrawingViews should require live Rhino.");
        RequireDrawingExportFailure(captureTool.CaptureDrawingExportState(filePath), "LIVE_RHINO_REQUIRED", "CaptureDrawingExportState should require live Rhino.");
        RequireDrawingExportFailure(styleTool.ApplyDrawingExportStyle(filePath, "missing"), "LIVE_RHINO_REQUIRED", "ApplyDrawingExportStyle should require live Rhino.");
        RequireDrawingExportFailure(backgroundTool.SetDrawingExportBackground(filePath, "missing"), "LIVE_RHINO_REQUIRED", "SetDrawingExportBackground should require live Rhino.");
        RequireDrawingExportFailure(restoreTool.RestoreDrawingExportState(filePath, "missing"), "LIVE_RHINO_REQUIRED", "RestoreDrawingExportState should require live Rhino.");
        RequireDrawingExportFailure(packageTool.ExportDrawingPackage(filePath, outputRoot), "LIVE_RHINO_REQUIRED", "ExportDrawingPackage should require live Rhino.");

        Console.WriteLine("Drawing export smoke test completed successfully (CLI fallback mode).");
    }

    private void RunLiveDrawingExportSmoke(string filePath)
    {
        string outputRoot = ResolveValidationDirectory("drawing-export-smoke-test");
        CleanupDrawingExportOutput(outputRoot);

        var setupTool = new SetupDrawingViewsTool(_drawingExportService);
        var packageTool = new ExportDrawingPackageTool(_drawingExportService);
        var restoreTool = new RestoreDrawingExportStateTool(_drawingExportService);

        OperationResponse<DrawingViewSetupResponse> needsLayer = setupTool.SetupDrawingViews(filePath);
        RequireDrawingExport(needsLayer.Success && needsLayer.Data is not null, "SetupDrawingViews should return a structured response.");
        RequireDrawingExport(needsLayer.Data!.Status == DrawingExportResponseStatus.NeedsLayerSelection, "SetupDrawingViews without layers should request layer selection.");
        RequireDrawingExport(needsLayer.Data.CandidateLayers.Count > 0, "SetupDrawingViews should return candidate layers.");

        string layerPath = needsLayer.Data.CandidateLayers
            .Where(candidate => candidate.ObjectCount > 0)
            .Select(candidate => candidate.FullPath)
            .FirstOrDefault()
            ?? needsLayer.Data.CandidateLayers[0].FullPath;

        OperationResponse<DrawingViewSetupResponse> setup = setupTool.SetupDrawingViews(
            filePath,
            confirmedLayerFullPaths: new List<string> { layerPath });
        RequireDrawingExport(setup.Success && setup.Data is not null, $"SetupDrawingViews failed: {setup.Message}");
        RequireDrawingExport(setup.Data!.ViewNames.Count == 8, "SetupDrawingViews should create/update 8 views.");
        RequireDrawingExport(AllNamedViewsExist(filePath, setup.Data.ViewNames), "All setup named views should exist in the live document.");

        DrawingExportSmokeSnapshot before = CaptureDrawingExportSmokeSnapshot(filePath);
        OperationResponse<DrawingExportPackageResponse> package = packageTool.ExportDrawingPackage(
            filePath,
            outputRoot,
            confirmedLayerFullPaths: new List<string> { layerPath },
            temporaryObjectColor: new ObjectColorRequest { R = 32, G = 96, B = 192 },
            imageSizePx: new ImageSizePxRequest { Width = 640, Height = 480 },
            pageSizeMm: new PageSizeMmRequest { WidthMm = 210d, HeightMm = 148d },
            dotsPerInch: 96d,
            viewNames: new List<string> { "MCP_Elevation_Front" });

        RequireDrawingExport(package.Success && package.Data is not null, $"ExportDrawingPackage failed: {package.Message}");
        RequireDrawingExport(package.Data!.ExportedFiles.Count == 2, "ExportDrawingPackage should export one PDF and one JPG for the requested view.");
        foreach (DrawingExportItemResponse exported in package.Data.ExportedFiles)
        {
            RequireDrawingExport(File.Exists(exported.OutputPath), $"Expected export file missing: {exported.OutputPath}");
            RequireDrawingExport(new FileInfo(exported.OutputPath).Length > 0, $"Expected export file to be non-empty: {exported.OutputPath}");
        }

        DrawingExportSmokeSnapshot after = CaptureDrawingExportSmokeSnapshot(filePath);
        RequireDrawingExport(before.Equals(after), "Drawing export workflow should restore object color and background state.");

        OperationResponse<DrawingExportStateResponse> missingRestore = restoreTool.RestoreDrawingExportState(filePath, "missing");
        RequireDrawingExportFailure(missingRestore, "DRAWING_EXPORT_SNAPSHOT_NOT_FOUND", "RestoreDrawingExportState should reject missing snapshot ids.");

        Console.WriteLine("Drawing export smoke test completed successfully (live mode).");
        Console.WriteLine($"Layer: {layerPath}");
        Console.WriteLine($"Output: {outputRoot}");
    }

    private bool AllNamedViewsExist(string filePath, IReadOnlyList<string> viewNames)
    {
        bool exists = false;
        _liveRhinoDocumentAccessor.Execute(filePath, document =>
        {
            exists = viewNames.All(viewName => document.NamedViews.FindByName(viewName) >= 0);
            return OperationResponse<bool>.Ok(true);
        });
        return exists;
    }

    private DrawingExportSmokeSnapshot CaptureDrawingExportSmokeSnapshot(string filePath)
    {
        DrawingExportSmokeSnapshot snapshot = new();
        _liveRhinoDocumentAccessor.Execute(filePath, document =>
        {
            RhinoObject? firstObject = document.Objects.FirstOrDefault(item => !item.IsDeleted);
            snapshot = new DrawingExportSmokeSnapshot
            {
                ObjectId = firstObject?.Id ?? Guid.Empty,
                ObjectColor = firstObject is null ? string.Empty : $"{firstObject.Attributes.ObjectColor.ToArgb()}:{firstObject.Attributes.ColorSource}",
                ViewportBackground = rhinocommon::Rhino.ApplicationSettings.AppearanceSettings.ViewportBackgroundColor.ToArgb(),
                RenderBackgroundTop = document.RenderSettings.BackgroundColorTop.ToArgb(),
                RenderBackgroundBottom = document.RenderSettings.BackgroundColorBottom.ToArgb()
            };
            return OperationResponse<bool>.Ok(true);
        });
        return snapshot;
    }

    private static void CleanupDrawingExportOutput(string outputRoot)
    {
        foreach (string filePath in Directory.EnumerateFiles(outputRoot, "MCP_*.*"))
        {
            File.Delete(filePath);
        }
    }

    private static void RequireDrawingExportFailure<T>(OperationResponse<T> response, string expectedMessageFragment, string message)
    {
        if (response.Success || !response.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{message} Actual: Success={response.Success}, Message={response.Message}");
        }
    }

    private static void RequireDrawingExport(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class DrawingExportSmokeSnapshot : IEquatable<DrawingExportSmokeSnapshot>
    {
        public Guid ObjectId { get; init; }
        public string ObjectColor { get; init; } = string.Empty;
        public int ViewportBackground { get; init; }
        public int RenderBackgroundTop { get; init; }
        public int RenderBackgroundBottom { get; init; }

        public bool Equals(DrawingExportSmokeSnapshot? other)
        {
            return other is not null
                && ObjectId == other.ObjectId
                && string.Equals(ObjectColor, other.ObjectColor, StringComparison.Ordinal)
                && ViewportBackground == other.ViewportBackground
                && RenderBackgroundTop == other.RenderBackgroundTop
                && RenderBackgroundBottom == other.RenderBackgroundBottom;
        }

        public override bool Equals(object? obj)
        {
            return obj is DrawingExportSmokeSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ObjectId, ObjectColor, ViewportBackground, RenderBackgroundTop, RenderBackgroundBottom);
        }
    }
}
