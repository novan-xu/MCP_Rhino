using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class ResolveObjectIdsInLiveTool
{
    private readonly RhinoObjectFilterService _filterService;

    public ResolveObjectIdsInLiveTool(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("按 ObjectId 在当前 Rhino 活动文档中解析物体，返回其图层、类型、user attributes 等元数据。需要文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。")]
    public OperationResponse<RhinoObjectFilterResult> ResolveObjectIdsInLive(
        string filePath,
        List<Guid> objectIds)
    {
        return _filterService.ResolveByObjectIdsInLive(filePath, objectIds ?? new List<Guid>());
    }
}
