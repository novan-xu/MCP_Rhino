using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoPrintScaleImageExportService
{
    private const int MaxViewCount = 16;
    private const int MaxDimensionPx = 16384;
    private const long MaxTotalPixels = 100_000_000L;
    private const double DefaultDpi = 300d;
    private const double DefaultFitScaleMultiplier = 1.08d;
    private const double DefaultMarginMm = 6d;
    private static readonly FileExportImageSize DefaultImageSize = new() { Width = 2400, Height = 2400 };
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(240);

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILivePrintScaleImageExporter _exporter;

    public RhinoPrintScaleImageExportService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILivePrintScaleImageExporter exporter)
    {
        _documentAccessor = documentAccessor;
        _exporter = exporter;
    }

    public OperationResponse<PrintScaleImageExportResponse> Export(ExportPrintScaleImagesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return OperationResponse<PrintScaleImageExportResponse>.Fail("FilePath is required.");
        }

        OperationResponse<PrintScaleImageExportSpec> validation = Validate(request);
        if (!validation.Success || validation.Data is null)
        {
            return OperationResponse<PrintScaleImageExportResponse>.Fail(validation.Message);
        }

        PrintScaleImageExportSpec spec = validation.Data;
        OperationResponse<PrintScaleImageExportResponse> response = _documentAccessor.Execute(request.FilePath, document =>
        {
            if (spec.Views.Any(view => PathsEqual(document.Path, view.OutputPath)))
            {
                return OperationResponse<PrintScaleImageExportResponse>.Fail("PRINT_SCALE_OUTPUT_OVERWRITE_BLOCKED");
            }

            OperationResponse<PrintScaleImageExportExecutionResult> exported = _exporter.Export(document, spec);
            if (!exported.Success || exported.Data is null)
            {
                return OperationResponse<PrintScaleImageExportResponse>.Fail(exported.Message);
            }

            PrintScaleImageExportExecutionResult result = exported.Data;
            return OperationResponse<PrintScaleImageExportResponse>.Ok(new PrintScaleImageExportResponse
            {
                FilePath = request.FilePath,
                AppliedModelScale = result.AppliedModelScale,
                WidthPx = spec.ImageSizePx.Width,
                HeightPx = spec.ImageSizePx.Height,
                DotsPerInch = spec.DotsPerInch,
                BackgroundRestored = result.BackgroundRestored,
                DurationMs = result.DurationMs,
                Images = result.Images.Select(image => new PrintScaleImageExportItemResponse
                {
                    ViewName = image.ViewName,
                    OutputPath = image.OutputPath,
                    OutputFileSizeBytes = image.OutputFileSizeBytes
                }).ToList(),
                Warnings = result.Warnings
            }, "Print-scale image export completed.");
        }, ExportTimeout);

        return !response.Success && string.Equals(response.Message, "RHINO_MAIN_THREAD_BUSY", StringComparison.Ordinal)
            ? OperationResponse<PrintScaleImageExportResponse>.Fail("PRINT_SCALE_EXPORT_TIMEOUT")
            : response;
    }

    private static OperationResponse<PrintScaleImageExportSpec> Validate(ExportPrintScaleImagesRequest request)
    {
        if (request.Views is null || request.Views.Count == 0)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("At least one named view is required.");
        }

        if (request.Views.Count > MaxViewCount)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail($"At most {MaxViewCount} views may be exported per call.");
        }

        int width = request.ImageSizePx?.Width ?? DefaultImageSize.Width;
        int height = request.ImageSizePx?.Height ?? DefaultImageSize.Height;
        if (width <= 0 || height <= 0 || width > MaxDimensionPx || height > MaxDimensionPx)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail($"ImageSizePx dimensions must be between 1 and {MaxDimensionPx}.");
        }

        if ((long)width * height * request.Views.Count > MaxTotalPixels)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail($"The request exceeds the {MaxTotalPixels} total-pixel limit.");
        }

        double dpi = request.DotsPerInch ?? DefaultDpi;
        if (!IsFinitePositive(dpi))
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("DotsPerInch must be a finite value greater than zero.");
        }

        double marginMm = request.MarginMm ?? DefaultMarginMm;
        if (!double.IsFinite(marginMm) || marginMm < 0d)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("MarginMm must be a finite value greater than or equal to zero.");
        }

        double mediaWidthMm = width / dpi * 25.4d;
        double mediaHeightMm = height / dpi * 25.4d;
        if (marginMm * 2d >= mediaWidthMm || marginMm * 2d >= mediaHeightMm)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("MarginMm leaves no printable image area.");
        }

        double fitMultiplier = request.FitScaleMultiplier ?? DefaultFitScaleMultiplier;
        if (!IsFinitePositive(fitMultiplier) || fitMultiplier < 1d || fitMultiplier > 10d)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("FitScaleMultiplier must be between 1.0 and 10.0; values above 1.0 add padding.");
        }

        if (request.ScaleMode == PrintScaleImageMode.Explicit && !IsFinitePositive(request.ModelScale))
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("ModelScale is required and must be finite and greater than zero for Explicit scale mode.");
        }

        if (request.ScaleMode == PrintScaleImageMode.FitAll && request.ModelScale.HasValue)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("ModelScale is only valid for Explicit scale mode.");
        }

        if (request.BackgroundTransparent && request.SolidBackgroundColor is not null)
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("SolidBackgroundColor cannot be combined with BackgroundTransparent.");
        }

        if (request.SolidBackgroundColor is not null
            && !IsColorChannel(request.SolidBackgroundColor.R, request.SolidBackgroundColor.G, request.SolidBackgroundColor.B))
        {
            return OperationResponse<PrintScaleImageExportSpec>.Fail("SolidBackgroundColor channels must be between 0 and 255.");
        }

        var normalizedViews = new List<PrintScaleImageViewSpec>(request.Views.Count);
        var seenViewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PrintScaleImageViewRequest view in request.Views)
        {
            string viewName = view.ViewName?.Trim() ?? string.Empty;
            if (viewName.Length == 0)
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail("Each viewName is required.");
            }

            if (!seenViewNames.Add(viewName))
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail($"Duplicate viewName is not allowed: {viewName}");
            }

            if (string.IsNullOrWhiteSpace(view.OutputPath) || !Path.IsPathFullyQualified(view.OutputPath))
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail("Each outputPath must be an absolute path.");
            }

            string outputPath = Path.GetFullPath(view.OutputPath);
            if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail("Print-scale image output supports only .png paths.");
            }

            string? parent = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail("PRINT_SCALE_OUTPUT_PARENT_NOT_FOUND");
            }

            if (!seenOutputPaths.Add(outputPath))
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail($"Duplicate outputPath is not allowed: {outputPath}");
            }

            if (File.Exists(outputPath) && !request.OverwriteExisting)
            {
                return OperationResponse<PrintScaleImageExportSpec>.Fail("PRINT_SCALE_OUTPUT_OVERWRITE_BLOCKED");
            }

            normalizedViews.Add(new PrintScaleImageViewSpec { ViewName = viewName, OutputPath = outputPath });
        }

        return OperationResponse<PrintScaleImageExportSpec>.Ok(new PrintScaleImageExportSpec
        {
            Views = normalizedViews,
            ImageSizePx = new FileExportImageSize { Width = width, Height = height },
            DotsPerInch = dpi,
            ScaleMode = request.ScaleMode,
            ModelScale = request.ModelScale,
            FitScaleMultiplier = fitMultiplier,
            MarginMm = marginMm,
            BackgroundTransparent = request.BackgroundTransparent,
            SolidBackgroundColor = request.SolidBackgroundColor is null
                ? null
                : new RhinoDisplayColor
                {
                    R = request.SolidBackgroundColor.R,
                    G = request.SolidBackgroundColor.G,
                    B = request.SolidBackgroundColor.B
                },
            OverwriteExisting = request.OverwriteExisting
        });
    }

    private static bool IsColorChannel(params int[] values)
    {
        return values.All(value => value is >= 0 and <= 255);
    }

    private static bool IsFinitePositive(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value) && value.Value > 0d;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
}
