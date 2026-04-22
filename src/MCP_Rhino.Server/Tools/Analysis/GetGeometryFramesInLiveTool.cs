using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetGeometryFramesInLiveTool
{
    private readonly RhinoGeometryMetricsService _service;

    public GetGeometryFramesInLiveTool(RhinoGeometryMetricsService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Inspect live curve start/end/tangent frames and surface normal/frame results from the active Rhino document. ObjectId only.")]
    public OperationResponse<GetGeometryFramesInLiveResponse> GetGeometryFramesInLive(string filePath, List<GeometryFrameEntryRequest> entries)
    {
        return _service.GetGeometryFramesInLive(new GetGeometryFramesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryFrameEntryRequest>()
        });
    }
}
