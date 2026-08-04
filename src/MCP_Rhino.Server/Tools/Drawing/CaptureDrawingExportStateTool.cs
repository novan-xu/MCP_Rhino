using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class CaptureDrawingExportStateTool
{
    private readonly RhinoDrawingExportService _service;

    public CaptureDrawingExportStateTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Capture exact object display attributes and drawing background state before staged drawing export. This is live-only.")]
    public OperationResponse<DrawingExportStateResponse> CaptureDrawingExportState(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _service.CaptureDrawingExportState(new CaptureDrawingExportStateRequest
        {
            FilePath = filePath,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
