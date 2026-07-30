using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Drawing;

public sealed class LotVisualizationExportSkill
{
    private const int DefaultWidthPx = 2400;
    private const int DefaultHeightPx = 2400;
    private const double DefaultDpi = 300d;
    private const double DefaultFitScaleMultiplier = 1.08d;
    private const double DefaultMarginMm = 6d;
    private const double DefaultViewFitMarginPercent = 10d;

    private static readonly string[] ViewNames =
    {
        "MCP_Iso_NE", "MCP_Iso_NW", "MCP_Iso_SE", "MCP_Iso_SW"
    };

    private readonly RhinoLotVisualizationService _lotService;
    private readonly RhinoDrawingExportService _drawingExportService;
    private readonly RhinoPrintScaleImageExportService _printScaleImageExportService;

    public LotVisualizationExportSkill(
        RhinoLotVisualizationService lotService,
        RhinoDrawingExportService drawingExportService,
        RhinoPrintScaleImageExportService printScaleImageExportService)
    {
        _lotService = lotService;
        _drawingExportService = drawingExportService;
        _printScaleImageExportService = printScaleImageExportService;
    }

    public OperationResponse<LotVisualizationPreviewResponse> Preview(PreviewLotVisualizationExportRequest request)
    {
        OperationResponse<PreparedOutput> prepared = PrepareOutput(
            request.FilePath,
            request.OutputDirectory,
            request.OutputFilePrefix,
            overwriteExisting: true,
            checkOverwrite: false);
        if (!prepared.Success || prepared.Data is null)
        {
            return OperationResponse<LotVisualizationPreviewResponse>.Fail(prepared.Message);
        }

        OperationResponse<LotVisualizationPreviewResponse> preview = _lotService.Preview(request);
        if (!preview.Success || preview.Data is null)
        {
            return preview;
        }

        preview.Data.ViewNames = ViewNames;
        preview.Data.OutputPaths = prepared.Data.Views.Select(view => view.OutputPath).ToList();
        return OperationResponse<LotVisualizationPreviewResponse>.Ok(preview.Data, "Lot visualization export preview resolved.");
    }

    public OperationResponse<LotVisualizationExportResponse> Export(ExportLotVisualizationRequest request)
    {
        OperationResponse<PreparedOutput> prepared = PrepareOutput(
            request.FilePath,
            request.OutputDirectory,
            request.OutputFilePrefix,
            request.OverwriteExisting,
            checkOverwrite: true);
        if (!prepared.Success || prepared.Data is null)
        {
            return OperationResponse<LotVisualizationExportResponse>.Fail(prepared.Message);
        }

        OperationResponse<LotVisualizationPreviewResponse> preview = _lotService.Preview(request);
        if (!preview.Success || preview.Data is null)
        {
            return OperationResponse<LotVisualizationExportResponse>.Fail(preview.Message);
        }

        preview.Data.ViewNames = ViewNames;
        preview.Data.OutputPaths = prepared.Data.Views.Select(view => view.OutputPath).ToList();

        OperationResponse<LotVisualizationApplyResult> applied = _lotService.Apply(request);
        if (!applied.Success || applied.Data is null)
        {
            return OperationResponse<LotVisualizationExportResponse>.Fail(applied.Message);
        }

        OperationResponse<DrawingViewSetupResponse> views = _drawingExportService.SetupDrawingViews(new SetupDrawingViewsRequest
        {
            FilePath = request.FilePath,
            ConfirmedLayerFullPaths = applied.Data.Plan.LayerFullPaths.ToList(),
            ObjectIds = applied.Data.Plan.TargetObjectIds,
            Preset = DrawingViewPreset.Isometric4,
            FitMarginPercent = request.ViewFitMarginPercent ?? DefaultViewFitMarginPercent
        });
        if (!views.Success || views.Data is null)
        {
            return OperationResponse<LotVisualizationExportResponse>.Fail(views.Message);
        }

        OperationResponse<PrintScaleImageExportResponse> exported = _printScaleImageExportService.Export(new ExportPrintScaleImagesRequest
        {
            FilePath = request.FilePath,
            Views = prepared.Data.Views,
            ImageSizePx = request.ImageSizePx ?? new ImageSizePxRequest { Width = DefaultWidthPx, Height = DefaultHeightPx },
            DotsPerInch = request.DotsPerInch ?? DefaultDpi,
            ScaleMode = PrintScaleImageMode.FitAll,
            FitScaleMultiplier = request.FitScaleMultiplier ?? DefaultFitScaleMultiplier,
            MarginMm = request.MarginMm ?? DefaultMarginMm,
            SolidBackgroundColor = new ObjectColorRequest { R = 255, G = 255, B = 255 },
            BackgroundTransparent = false,
            OverwriteExisting = request.OverwriteExisting
        });
        if (!exported.Success || exported.Data is null)
        {
            return OperationResponse<LotVisualizationExportResponse>.Fail(exported.Message);
        }

        var warnings = new List<ObjectEditWarning>();
        warnings.AddRange(applied.Data.Plan.Warnings);
        warnings.AddRange(views.Data.Warnings);
        warnings.AddRange(exported.Data.Warnings);

        return OperationResponse<LotVisualizationExportResponse>.Ok(new LotVisualizationExportResponse
        {
            FilePath = request.FilePath,
            Plan = preview.Data,
            ModifiedObjectCount = applied.Data.ModifiedObjectCount,
            CreatedGroupCount = applied.Data.CreatedGroupCount,
            ReusedGroupCount = applied.Data.ReusedGroupCount,
            CreatedViewNames = views.Data.CreatedViewNames,
            UpdatedViewNames = views.Data.UpdatedViewNames,
            AppliedModelScale = exported.Data.AppliedModelScale,
            BackgroundRestored = exported.Data.BackgroundRestored,
            Images = exported.Data.Images,
            Warnings = warnings
        }, "Lot visualization grouped, colored, viewed, and exported at one print scale.");
    }

    private static OperationResponse<PreparedOutput> PrepareOutput(
        string filePath,
        string? outputDirectory,
        string? outputFilePrefix,
        bool overwriteExisting,
        bool checkOverwrite)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
        {
            return OperationResponse<PreparedOutput>.Fail("FilePath must be an absolute saved .3dm path.");
        }

        string directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty
            : Path.GetFullPath(outputDirectory);
        if (directory.Length == 0 || !Directory.Exists(directory))
        {
            return OperationResponse<PreparedOutput>.Fail("LOT_VISUALIZATION_OUTPUT_PARENT_NOT_FOUND");
        }

        string prefix = string.IsNullOrWhiteSpace(outputFilePrefix)
            ? Path.GetFileNameWithoutExtension(filePath)
            : outputFilePrefix.Trim();
        if (prefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return OperationResponse<PreparedOutput>.Fail("OutputFilePrefix contains invalid file-name characters.");
        }

        IReadOnlyList<PrintScaleImageViewRequest> views = ViewNames.Select(viewName =>
        {
            string suffix = viewName["MCP_".Length..];
            return new PrintScaleImageViewRequest
            {
                ViewName = viewName,
                OutputPath = Path.Combine(directory, $"{prefix}_{suffix}_print.png")
            };
        }).ToList();

        if (checkOverwrite && !overwriteExisting && views.Any(view => File.Exists(view.OutputPath)))
        {
            return OperationResponse<PreparedOutput>.Fail("PRINT_SCALE_OUTPUT_OVERWRITE_BLOCKED");
        }

        return OperationResponse<PreparedOutput>.Ok(new PreparedOutput { Views = views });
    }

    private sealed class PreparedOutput
    {
        public IReadOnlyList<PrintScaleImageViewRequest> Views { get; init; } = Array.Empty<PrintScaleImageViewRequest>();
    }
}
