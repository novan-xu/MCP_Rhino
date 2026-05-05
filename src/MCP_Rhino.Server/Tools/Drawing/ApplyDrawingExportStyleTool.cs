using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class ApplyDrawingExportStyleTool
{
    private readonly RhinoDrawingExportService _service;

    public ApplyDrawingExportStyleTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Apply temporary object styling for drawing export from a captured drawing export snapshot. This is live-only and must be restored with RestoreDrawingExportState.")]
    public OperationResponse<DrawingExportStateResponse> ApplyDrawingExportStyle(
        string filePath,
        string snapshotId,
        ObjectColorRequest? objectColor = null)
    {
        return _service.ApplyDrawingExportStyle(new ApplyDrawingExportStyleRequest
        {
            FilePath = filePath,
            SnapshotId = snapshotId,
            ObjectColor = objectColor
        });
    }
}
