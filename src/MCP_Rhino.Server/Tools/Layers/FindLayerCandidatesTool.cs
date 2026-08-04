using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class FindLayerCandidatesTool
{
    private readonly RhinoObjectFilterService _filterService;

    public FindLayerCandidatesTool(RhinoObjectFilterService filterService)
    {
        _filterService = filterService;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Find live Rhino layer candidates by name or full-path fragment so later calls can use confirmedLayerFullPaths without ambiguity.")]
    public OperationResponse<IReadOnlyList<RhinoLayerCandidate>> FindLayerCandidates(
        string filePath,
        string layerQuery,
        bool exactMatch = false)
    {
        return _filterService.FindLayerCandidates(new FindLayerCandidatesRequest
        {
            FilePath = filePath,
            LayerQuery = layerQuery,
            ExactMatch = exactMatch
        });
    }
}
