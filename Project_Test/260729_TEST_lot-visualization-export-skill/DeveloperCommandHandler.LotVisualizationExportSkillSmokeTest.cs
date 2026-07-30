extern alias rhinocommon;

using System.Drawing;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using MCP_Rhino.Server.Domain.Models;
using AppearanceSettings = rhinocommon::Rhino.ApplicationSettings.AppearanceSettings;
using BackgroundStyle = rhinocommon::Rhino.Display.BackgroundStyle;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string LotVisualizationExportSkillSlug = "lot-visualization-export-skill-smoke-test";

    partial void RegisterLotVisualizationExportSkillHandlers()
    {
        _extensionHandlers[LotVisualizationExportSkillSlug] = HandleLotVisualizationExportSkillSmokeTest;
    }

    private bool HandleLotVisualizationExportSkillSmokeTest(string[] args)
    {
        try
        {
            string filePath = args.Length > 1
                ? Path.GetFullPath(args[1])
                : "C:/mcp-rhino/lot-visualization-export-skill-smoke.3dm";
            RunLotVisualizationSourceContractSmoke();
            RunLotVisualizationDeterminismSmoke();
            if (McpRhinoPlugin.Instance is null)
            {
                RunLotVisualizationCliFallbackSmoke(filePath);
            }
            else
            {
                RunLotVisualizationLiveBackgroundSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Lot visualization export skill smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunLotVisualizationSourceContractSmoke()
    {
        string root = Directory.GetCurrentDirectory();
        string drawingOperatorPath = Path.Combine(
            root, "src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveDrawingExportStateOperator.cs");
        string printExporterPath = Path.Combine(
            root, "src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveRhinoPrintScaleImageExporter.cs");
        RequireLotVisualization(File.Exists(drawingOperatorPath), "Drawing export state operator source was not found.");
        RequireLotVisualization(File.Exists(printExporterPath), "Print-scale exporter source was not found.");

        string drawingSource = File.ReadAllText(drawingOperatorPath);
        string printSource = File.ReadAllText(printExporterPath);
        RequireLotVisualization(
            !drawingSource.Contains("AppearanceSettings", StringComparison.Ordinal),
            "Drawing export background staging must not use Rhino application appearance settings.");
        RequireLotVisualization(
            !printSource.Contains("AppearanceSettings", StringComparison.Ordinal),
            "Print-scale background staging must not use Rhino application appearance settings.");
        RequireLotVisualization(
            printSource.Contains("BackgroundStyle.SolidColor", StringComparison.Ordinal),
            "Print-scale white background must use the document solid background option.");
        RequireLotVisualization(
            printSource.Contains("RestoreRenderBackground", StringComparison.Ordinal),
            "Print-scale background staging must have an explicit restore path.");
        RequireLotVisualization(
            Enum.IsDefined(DrawingViewPreset.Isometric4),
            "The four-isometric drawing preset must be registered.");

        Console.WriteLine("[OK] lot visualization source contracts verified.");
    }

    private static void RunLotVisualizationDeterminismSmoke()
    {
        Type operatorType = typeof(LiveLotVisualizationOperator);
        MethodInfo createColor = operatorType.GetMethod("CreateColor", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Lot color policy method was not found.");
        MethodInfo createGroupName = operatorType.GetMethod("CreateGroupName", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Lot group naming policy method was not found.");
        MethodInfo isLotKey = operatorType.GetMethod("IsConservativeLotKey", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Lot-key policy method was not found.");

        var colors = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < 32; index++)
        {
            var color = (RhinoDisplayColor)(createColor.Invoke(null, new object[] { index })
                ?? throw new InvalidOperationException("Lot color policy returned null."));
            RequireLotVisualization(colors.Add($"{color.R},{color.G},{color.B}"), $"Lot color {index} was not distinct.");
        }

        string firstName = (string)(createGroupName.Invoke(null, new object[] { "MCP_Lot_", "REL-BKT-PNL-01.00" })
            ?? throw new InvalidOperationException("Lot group naming policy returned null."));
        string repeatedName = (string)(createGroupName.Invoke(null, new object[] { "MCP_Lot_", "REL-BKT-PNL-01.00" })
            ?? throw new InvalidOperationException("Lot group naming policy returned null."));
        string secondName = (string)(createGroupName.Invoke(null, new object[] { "MCP_Lot_", "REL-BKT-PNL-02.00" })
            ?? throw new InvalidOperationException("Lot group naming policy returned null."));
        RequireLotVisualization(firstName == repeatedName, "Lot group naming should be deterministic.");
        RequireLotVisualization(firstName != secondName, "Different lots should receive different group names.");
        RequireLotVisualization(firstName.StartsWith("MCP_Lot_", StringComparison.Ordinal), "Skill-owned group prefix was not preserved.");

        RequireLotVisualization((bool)isLotKey.Invoke(null, new object[] { "Element Lot" })!, "Element Lot should be detected as a lot key.");
        RequireLotVisualization((bool)isLotKey.Invoke(null, new object[] { "Lot Number" })!, "Lot Number should be detected as a lot key.");
        RequireLotVisualization(!(bool)isLotKey.Invoke(null, new object[] { "Pilot Status" })!, "Pilot Status must not be detected as a lot key.");

        Console.WriteLine("[OK] deterministic lot colors, groups, and key detection verified.");
    }

    private void RunLotVisualizationCliFallbackSmoke(string filePath)
    {
        string outputRoot = ResolveValidationDirectory(LotVisualizationExportSkillSlug);
        Directory.CreateDirectory(outputRoot);

        OperationResponse<LotVisualizationPreviewResponse> preview = _lotVisualizationExportSkill.Preview(
            new PreviewLotVisualizationExportRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                LotNumberKeys = new[] { "Lot Number" }
            });
        RequireLotVisualizationFailure(preview, "LIVE_RHINO_REQUIRED", "CLI preview should require live Rhino.");

        OperationResponse<LotVisualizationExportResponse> export = _lotVisualizationExportSkill.Export(
            new ExportLotVisualizationRequest
            {
                FilePath = filePath,
                OutputDirectory = outputRoot,
                OutputFilePrefix = "cli-fallback",
                LotNumberKeys = new[] { "Lot Number" },
                ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 320 },
                OverwriteExisting = false
            });
        RequireLotVisualizationFailure(export, "LIVE_RHINO_REQUIRED", "CLI export should require live Rhino.");
        RequireLotVisualization(
            !Directory.EnumerateFiles(outputRoot, "cli-fallback_*.png").Any(),
            "CLI fallback must not write PNG files.");

        Console.WriteLine("[OK] lot visualization CLI fallback verified.");
    }

    private void RunLotVisualizationLiveBackgroundSmoke(string filePath)
    {
        RhinoDoc document = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("Live smoke requires an active Rhino document.");
        RequireLotVisualization(
            string.Equals(Path.GetFullPath(document.Path), filePath, StringComparison.OrdinalIgnoreCase),
            "Live smoke file path must match the active saved document.");

        OperationResponse<LotVisualizationPreviewResponse> preview = _lotVisualizationExportSkill.Preview(
            new PreviewLotVisualizationExportRequest { FilePath = filePath });
        RequireLotVisualization(preview.Success && preview.Data is not null, $"Live lot preview failed: {preview.Message}");
        RequireLotVisualization(preview.Data!.TargetObjectCount > 0, "Live lot preview should resolve visible geometry.");
        RequireLotVisualization(preview.Data.ViewNames.Count == 4, "Lot preview should plan exactly four isometric views.");

        string outputRoot = ResolveValidationDirectory(LotVisualizationExportSkillSlug);
        Directory.CreateDirectory(outputRoot);
        string outputPath = Path.Combine(outputRoot, "temporary-solid-background.png");
        TryDeleteLotVisualizationOutput(outputPath);

        Color applicationBackgroundBefore = AppearanceSettings.ViewportBackgroundColor;
        BackgroundStyle renderStyleBefore = document.RenderSettings.BackgroundStyle;
        Color renderTopBefore = document.RenderSettings.BackgroundColorTop;
        Color renderBottomBefore = document.RenderSettings.BackgroundColorBottom;

        try
        {
            string viewName = document.Views.ActiveView?.MainViewport.Name
                ?? throw new InvalidOperationException("Live smoke requires an active viewport.");
            OperationResponse<PrintScaleImageExportResponse> export = _printScaleImageExportService.Export(
                new ExportPrintScaleImagesRequest
                {
                    FilePath = filePath,
                    Views = new[]
                    {
                        new PrintScaleImageViewRequest { ViewName = viewName, OutputPath = outputPath }
                    },
                    ImageSizePx = new ImageSizePxRequest { Width = 320, Height = 320 },
                    DotsPerInch = 96d,
                    ScaleMode = PrintScaleImageMode.FitAll,
                    FitScaleMultiplier = 1.1d,
                    MarginMm = 3d,
                    SolidBackgroundColor = new ObjectColorRequest { R = 255, G = 255, B = 255 },
                    OverwriteExisting = false
                });
            RequireLotVisualization(export.Success, $"Temporary white-background export failed: {export.Message}");
            RequireLotVisualization(export.Data?.BackgroundRestored == true, "Exporter should report successful background restoration.");
            RequireLotVisualization(File.Exists(outputPath) && new FileInfo(outputPath).Length > 0L, "Temporary export PNG was not written.");

            RequireLotVisualization(
                AppearanceSettings.ViewportBackgroundColor.ToArgb() == applicationBackgroundBefore.ToArgb(),
                "Rhino application viewport background changed during export.");
            RequireLotVisualization(document.RenderSettings.BackgroundStyle == renderStyleBefore, "Document render background style was not restored.");
            RequireLotVisualization(document.RenderSettings.BackgroundColorTop.ToArgb() == renderTopBefore.ToArgb(), "Document render top color was not restored.");
            RequireLotVisualization(document.RenderSettings.BackgroundColorBottom.ToArgb() == renderBottomBefore.ToArgb(), "Document render bottom color was not restored.");

            File.WriteAllLines(Path.Combine(outputRoot, "live-background-result.txt"), new[]
            {
                $"file={filePath}",
                $"targetObjects={preview.Data.TargetObjectCount}",
                $"lots={preview.Data.Lots.Count}",
                $"applicationBackgroundBefore={applicationBackgroundBefore.ToArgb()}",
                $"applicationBackgroundAfter={AppearanceSettings.ViewportBackgroundColor.ToArgb()}",
                $"renderStyleBefore={renderStyleBefore}",
                $"renderStyleAfter={document.RenderSettings.BackgroundStyle}",
                $"renderTopBefore={renderTopBefore.ToArgb()}",
                $"renderTopAfter={document.RenderSettings.BackgroundColorTop.ToArgb()}",
                $"renderBottomBefore={renderBottomBefore.ToArgb()}",
                $"renderBottomAfter={document.RenderSettings.BackgroundColorBottom.ToArgb()}",
                $"backgroundRestored={export.Data?.BackgroundRestored == true}"
            });
        }
        finally
        {
            TryDeleteLotVisualizationOutput(outputPath);
        }

        Console.WriteLine("[OK] live temporary solid background restored without changing application appearance.");
    }

    private static void RequireLotVisualizationFailure<T>(
        OperationResponse<T> response,
        string expectedMessageFragment,
        string message)
    {
        RequireLotVisualization(
            !response.Success && response.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase),
            $"{message} Actual: Success={response.Success}, Message={response.Message}");
    }

    private static void TryDeleteLotVisualizationOutput(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RequireLotVisualization(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
