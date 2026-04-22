using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetObjectMetricsInLiveTool
{
    private readonly RhinoGeometryMetricsService _service;

    public GetObjectMetricsInLiveTool(RhinoGeometryMetricsService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Read live Rhino object metrics including length, area, perimeter, volume, and closed state. Requires the saved active document and has no offline fallback.")]
    public OperationResponse<GetObjectMetricsInLiveResponse> GetObjectMetricsInLive(string filePath, List<Guid> objectIds)
    {
        return _service.GetObjectMetricsInLive(new GetObjectMetricsInLiveRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}
