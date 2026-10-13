using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string PrintScaleImageExportSlug = "print-scale-image-export-smoke-test";

    partial void RegisterPrintScaleImageExportHandlers()
    {
        _extensionHandlers[PrintScaleImageExportSlug] = HandlePrintScaleImageExportSmokeTest;
    }

    private bool HandlePrintScaleImageExportSmokeTest(string[] args)
    {
        try
        {
            string filePath = args.Length > 1
                ? Path.GetFullPath(args[1])
                : "C:/mcp-rhino/print-scale-image-export-smoke.3dm";

            RunPrintScaleImageExportValidationSmoke(filePath);
            if (McpRhinoPlugin.Instance is null)
            {
                RunPrintScaleImageExportCliFallbackSmoke(filePath);
            }
            else
            {
                RunPrintScaleImageExportLiveSmoke(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Print-scale image export smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunPrintScaleImageExportValidationSmoke(string filePath)
    {
        string outputRoot = PreparePrintScaleImageExportOutputRoot();

        RequirePrintScaleImageFailure(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                Views = Array.Empty<PrintScaleImageViewRequest>()
            }),
            "At least one named view",
            "Empty view batches should fail validation.");

        RequirePrintScaleImageFailure(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                ScaleMode = PrintScaleImageMode.Explicit,
                Views = new[]
                {
                    new PrintScaleImageViewRequest
                    {
                        ViewName = "MCP_Iso_NE",
                        OutputPath = Path.Combine(outputRoot, "explicit-missing-scale.png")
                    }
                }
            }),
            "ModelScale is required",
            "Explicit mode without a model scale should fail validation.");

        RequirePrintScaleImageFailure(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                Views = new[]
                {
                    new PrintScaleImageViewRequest
                    {
                        ViewName = "MCP_Iso_NE",
                        OutputPath = Path.Combine(outputRoot, "invalid-extension.jpg")
                    }
                }
            }),
            "only .png",
            "Non-PNG output should fail validation.");

        Console.WriteLine("[OK] print-scale image export validation smoke completed.");
    }

    private void RunPrintScaleImageExportCliFallbackSmoke(string filePath)
    {
        string outputRoot = PreparePrintScaleImageExportOutputRoot();
        string outputPath = Path.Combine(outputRoot, "cli-fallback.png");
        TryDeletePrintScaleImageOutput(outputPath);

        RequirePrintScaleImageFailure(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                Views = new[]
                {
                    new PrintScaleImageViewRequest
                    {
                        ViewName = "MCP_Iso_NE",
                        OutputPath = outputPath
                    }
                }
            }),
            "LIVE_RHINO_REQUIRED",
            "CLI fallback should require live Rhino.");
        RequirePrintScaleImage(!File.Exists(outputPath), "CLI fallback must not write output.");

        Console.WriteLine("[OK] print-scale image export CLI fallback smoke completed.");
    }

    private void RunPrintScaleImageExportLiveSmoke(string filePath)
    {
        RequirePrintScaleImage(File.Exists(filePath), "Live smoke requires a saved active document.");
        string outputRoot = PreparePrintScaleImageExportOutputRoot();
        string fitNe = Path.Combine(outputRoot, "print-scale-fit-ne.png");
        string fitSw = Path.Combine(outputRoot, "print-scale-fit-sw.png");
        string explicitNe = Path.Combine(outputRoot, "print-scale-explicit-ne.png");
        string explicitSw = Path.Combine(outputRoot, "print-scale-explicit-sw.png");
        foreach (string path in new[] { fitNe, fitSw, explicitNe, explicitSw })
        {
            TryDeletePrintScaleImageOutput(path);
        }

        PrintScaleImageExportResponse fit = RequirePrintScaleImageSuccess(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                Views = new[]
                {
                    new PrintScaleImageViewRequest { ViewName = "MCP_Iso_NE", OutputPath = fitNe },
                    new PrintScaleImageViewRequest { ViewName = "MCP_Iso_SW", OutputPath = fitSw }
                },
                ImageSizePx = new ImageSizePxRequest { Width = 640, Height = 640 },
                DotsPerInch = 150d,
                ScaleMode = PrintScaleImageMode.FitAll,
                FitScaleMultiplier = 1.1d,
                MarginMm = 4d,
                OverwriteExisting = false
            }),
            "FitAll live export");

        RequirePrintScaleImage(fit.AppliedModelScale > 0d, "FitAll should report a positive common scale.");
        RequirePrintScaleImage(fit.Images.Count == 2, "FitAll should export both views.");
        foreach (PrintScaleImageExportItemResponse image in fit.Images)
        {
            RequirePrintScaleImageOutput(image.OutputPath, "FitAll output should be non-empty.");
        }

        PrintScaleImageExportResponse explicitResult = RequirePrintScaleImageSuccess(
            _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
            {
                FilePath = filePath,
                Views = new[]
                {
                    new PrintScaleImageViewRequest { ViewName = "MCP_Iso_NE", OutputPath = explicitNe },
                    new PrintScaleImageViewRequest { ViewName = "MCP_Iso_SW", OutputPath = explicitSw }
                },
                ImageSizePx = new ImageSizePxRequest { Width = 640, Height = 640 },
                DotsPerInch = 150d,
                ScaleMode = PrintScaleImageMode.Explicit,
                ModelScale = fit.AppliedModelScale,
                MarginMm = 4d,
                OverwriteExisting = false
            }),
            "Explicit live export");

        RequirePrintScaleImage(
            Math.Abs(explicitResult.AppliedModelScale - fit.AppliedModelScale) <= 1e-12,
            "Explicit export should reuse the exact FitAll scale.");
        foreach (PrintScaleImageExportItemResponse image in explicitResult.Images)
        {
            RequirePrintScaleImageOutput(image.OutputPath, "Explicit output should be non-empty.");
        }

        Console.WriteLine("[OK] print-scale image export live smoke completed.");
        Console.WriteLine($"[OK] commonModelScale={fit.AppliedModelScale:R}; outputRoot={outputRoot}");
    }

    private static string PreparePrintScaleImageExportOutputRoot()
    {
        string outputRoot = ResolveValidationDirectory(PrintScaleImageExportSlug);
        Directory.CreateDirectory(outputRoot);
        return outputRoot;
    }

    private static PrintScaleImageExportResponse RequirePrintScaleImageSuccess(
        OperationResponse<PrintScaleImageExportResponse> response,
        string label)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{label} failed: {response.Message}");
        }

        return response.Data;
    }

    private static void RequirePrintScaleImageFailure(
        OperationResponse<PrintScaleImageExportResponse> response,
        string expectedMessageFragment,
        string message)
    {
        if (response.Success || !response.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{message} Actual: Success={response.Success}, Message={response.Message}");
        }
    }

    private static void RequirePrintScaleImageOutput(string outputPath, string message)
    {
        RequirePrintScaleImage(File.Exists(outputPath), message);
        RequirePrintScaleImage(new FileInfo(outputPath).Length > 0L, message);
    }

    private static void TryDeletePrintScaleImageOutput(string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    private static void RequirePrintScaleImage(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
