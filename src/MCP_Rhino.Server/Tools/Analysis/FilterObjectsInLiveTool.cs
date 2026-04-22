using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class FilterObjectsInLiveTool
{
    private readonly RhinoObjectFilterService _filterService;

    public FilterObjectsInLiveTool(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    [McpServerTool]
    [Description("针对当前 Rhino 活动文档按图层/类型/用户属性等条件筛选物体。需要文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。离线筛选请用 filter-objects。")]
    public OperationResponse<RhinoObjectFilterResult> FilterObjectsInLive(
        string filePath,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _filterService.FilterInLive(new FilterObjectsRequest
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
