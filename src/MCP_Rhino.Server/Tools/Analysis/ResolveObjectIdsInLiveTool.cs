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
    [Description("Resolve explicit object ids in the current live Rhino document and return layer, type, name, and user attribute metadata.")]
    public OperationResponse<RhinoObjectFilterResult> ResolveObjectIdsInLive(
        string filePath,
        List<Guid> objectIds)
    {
        return _filterService.ResolveByObjectIdsInLive(filePath, objectIds ?? new List<Guid>());
    }
}
