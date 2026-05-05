using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class RestoreDrawingExportStateTool
{
    private readonly RhinoDrawingExportService _service;

    public RestoreDrawingExportStateTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Restore object display attributes and drawing background from a captured drawing export snapshot. This is live-only.")]
    public OperationResponse<DrawingExportStateResponse> RestoreDrawingExportState(string filePath, string snapshotId)
    {
        return _service.RestoreDrawingExportState(new RestoreDrawingExportStateRequest
        {
            FilePath = filePath,
            SnapshotId = snapshotId
        });
    }
}
