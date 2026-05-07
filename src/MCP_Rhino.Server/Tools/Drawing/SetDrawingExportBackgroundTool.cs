using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Drawing;

[McpServerToolType]
public sealed class SetDrawingExportBackgroundTool
{
    private readonly RhinoDrawingExportService _service;

    public SetDrawingExportBackgroundTool(RhinoDrawingExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Set drawing export background color after capturing a drawing export snapshot. Defaults to white. This is live-only.")]
    public OperationResponse<DrawingExportStateResponse> SetDrawingExportBackground(
        string filePath,
        string snapshotId,
        ObjectColorRequest? color = null)
    {
        return _service.SetDrawingExportBackground(new SetDrawingExportBackgroundRequest
        {
            FilePath = filePath,
            SnapshotId = snapshotId,
            Color = color
        });
    }
}
