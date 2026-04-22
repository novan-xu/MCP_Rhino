using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class FindLayerCandidatesInLiveTool
{
    private readonly RhinoObjectFilterService _filterService;

    public FindLayerCandidatesInLiveTool(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    [McpServerTool]
    [Description("在当前 Rhino 活动文档中按名称或路径片段搜索图层。需要该文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。")]
    public OperationResponse<IReadOnlyList<RhinoLayerCandidate>> FindLayerCandidatesInLive(
        string filePath,
        string layerQuery,
        bool exactMatch = false)
    {
        return _filterService.FindLayerCandidatesInLive(new FindLayerCandidatesRequest
        {
            FilePath = filePath,
            LayerQuery = layerQuery,
            ExactMatch = exactMatch
        });
    }
}
