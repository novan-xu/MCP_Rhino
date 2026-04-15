using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class FindLayerCandidatesTool
{
    private readonly RhinoObjectFilterService _filterService;

    public FindLayerCandidatesTool(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    [McpServerTool]
    [Description("根据图层名称或层级路径查找 Rhino 文件中的匹配图层候选，用于后续筛查时确认目标图层。")]
    public string FindLayerCandidates(string filePath, string layerQuery, bool exactMatch = false)
    {
        var result = _filterService.FindLayerCandidates(new FindLayerCandidatesRequest
        {
            FilePath = filePath,
            LayerQuery = layerQuery,
            ExactMatch = exactMatch
        });

        return _filterService.FormatLayerCandidates(layerQuery, result.Success ? result.Data : null, result.Message);
    }
}