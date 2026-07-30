using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class ExportPrintScaleImagesTool
{
    private readonly RhinoPrintScaleImageExportService _service;

    public ExportPrintScaleImagesTool(RhinoPrintScaleImageExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Export named Rhino views to PNG through Rhino Print settings. Named views control camera angles only; all outputs share one explicit or common fit model scale, use visible-geometry extents, and are rejected if blank. An explicit modelScale is the same-unit model-to-page denominator (100 means 1:100); FitAll reports the denominator it applied. This is live-only and does not mutate the Rhino document.")]
    public OperationResponse<PrintScaleImageExportResponse> ExportPrintScaleImages(
        string filePath,
        IReadOnlyList<PrintScaleImageViewRequest> views,
        ImageSizePxRequest? imageSizePx = null,
        double? dotsPerInch = null,
        PrintScaleImageMode scaleMode = PrintScaleImageMode.FitAll,
        double? modelScale = null,
        double? fitScaleMultiplier = null,
        double? marginMm = null,
        bool backgroundTransparent = false,
        ObjectColorRequest? solidBackgroundColor = null,
        bool overwriteExisting = false)
    {
        return _service.Export(new ExportPrintScaleImagesRequest
        {
            FilePath = filePath,
            Views = views,
            ImageSizePx = imageSizePx,
            DotsPerInch = dotsPerInch,
            ScaleMode = scaleMode,
            ModelScale = modelScale,
            FitScaleMultiplier = fitScaleMultiplier,
            MarginMm = marginMm,
            BackgroundTransparent = backgroundTransparent,
            SolidBackgroundColor = solidBackgroundColor,
            OverwriteExisting = overwriteExisting
        });
    }
}
