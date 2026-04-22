using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class GetLayersInLiveTool
{
    private readonly RhinoLayerManagementService _service;

    public GetLayersInLiveTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("读取当前在 Rhino 中打开的文档的图层表。需要该文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。离线读请用 get-layers。")]
    public OperationResponse<LayerReadResponse> GetLayersInLive(string filePath)
    {
        return _service.GetInLive(new Contracts.Requests.GetLayersRequest { FilePath = filePath });
    }
}
