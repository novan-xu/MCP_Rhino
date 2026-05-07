using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class IntersectObjectsInLiveTool
{
    private readonly RhinoGeometryIntersectionService _service;

    public IntersectObjectsInLiveTool(RhinoGeometryIntersectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Intersect live Rhino geometry pairs. Supports ObjectId and temporary geometry specs for each entry, with tolerance defaulting to the active document tolerance.")]
    public OperationResponse<IntersectObjectsInLiveResponse> IntersectObjectsInLive(string filePath, List<GeometryIntersectionEntryRequest> entries)
    {
        return _service.IntersectObjectsInLive(new IntersectObjectsInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryIntersectionEntryRequest>()
        });
    }
}
