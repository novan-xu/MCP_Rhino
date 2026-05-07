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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect live curve and surface frames from the active Rhino document. Supports legacy single-frame entries plus ParameterSpec batch sampling. ObjectId only.")]
    public OperationResponse<GetGeometryFramesInLiveResponse> GetGeometryFramesInLive(string filePath, List<GeometryFrameEntryRequest> entries)
    {
        return _service.GetGeometryFramesInLive(new GetGeometryFramesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryFrameEntryRequest>()
        });
    }
}
