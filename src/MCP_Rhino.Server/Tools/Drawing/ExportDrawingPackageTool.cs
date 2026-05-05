using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class ExportDrawingPackageTool
{
    private readonly RhinoDrawingExportService _service;

    public ExportDrawingPackageTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Set up layer-fitted drawing views, apply temporary styling/background, export PDF/JPG drawings, and restore exact state. This is live-only.")]
    public OperationResponse<DrawingExportPackageResponse> ExportDrawingPackage(
        string filePath,
        string outputDirectory,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        ObjectColorRequest? temporaryObjectColor = null,
        bool exportPdf = true,
        bool exportJpg = true,
        ImageSizePxRequest? imageSizePx = null,
        PageSizeMmRequest? pageSizeMm = null,
        double? dotsPerInch = null,
        bool overwriteExisting = true,
        List<string>? viewNames = null,
        DrawingViewPreset preset = DrawingViewPreset.Standard8,
        double? fitMarginPercent = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _service.ExportDrawingPackage(new ExportDrawingPackageRequest
        {
            FilePath = filePath,
            OutputDirectory = outputDirectory,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            TemporaryObjectColor = temporaryObjectColor,
            ExportPdf = exportPdf,
            ExportJpg = exportJpg,
            ImageSizePx = imageSizePx,
            PageSizeMm = pageSizeMm,
            DotsPerInch = dotsPerInch,
            OverwriteExisting = overwriteExisting,
            ViewNames = viewNames ?? new List<string>(),
            Preset = preset,
            FitMarginPercent = fitMarginPercent,
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
