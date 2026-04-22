using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetClosestPointsInLiveTool
{
    private readonly RhinoGeometryIntersectionService _service;

    public GetClosestPointsInLiveTool(RhinoGeometryIntersectionService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Get live closest-point results between a point-like source and Rhino geometry targets. Supports ObjectId and temporary geometry specs for each entry.")]
    public OperationResponse<GetClosestPointsInLiveResponse> GetClosestPointsInLive(string filePath, List<GeometryClosestPointEntryRequest> entries)
    {
        return _service.GetClosestPointsInLive(new GetClosestPointsInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryClosestPointEntryRequest>()
        });
    }
}
