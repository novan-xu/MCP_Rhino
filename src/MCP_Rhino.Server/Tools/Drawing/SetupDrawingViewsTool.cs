using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class SetupDrawingViewsTool
{
    private readonly RhinoDrawingExportService _service;

    public SetupDrawingViewsTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create or update standard layer-fitted named drawing views for PDF/JPG export. Returns candidate layers when layer selection is missing. This is live-only.")]
    public OperationResponse<DrawingViewSetupResponse> SetupDrawingViews(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        IReadOnlyList<Guid>? objectIds = null,
        DrawingViewPreset preset = DrawingViewPreset.Standard8,
        double? fitMarginPercent = null)
    {
        return _service.SetupDrawingViews(new SetupDrawingViewsRequest
        {
            FilePath = filePath,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectIds = objectIds ?? Array.Empty<Guid>(),
            Preset = preset,
            FitMarginPercent = fitMarginPercent
        });
    }
}
