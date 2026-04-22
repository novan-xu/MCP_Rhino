using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class MeasureDistancesInLiveTool
{
    private readonly RhinoGeometryMetricsService _service;

    public MeasureDistancesInLiveTool(RhinoGeometryMetricsService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Measure live distances between point-like references and Rhino geometry. Supports ObjectId and temporary geometry specs for each entry.")]
    public OperationResponse<MeasureDistancesInLiveResponse> MeasureDistancesInLive(string filePath, List<MeasureDistanceEntryRequest> entries)
    {
        return _service.MeasureDistancesInLive(new MeasureDistancesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<MeasureDistanceEntryRequest>()
        });
    }
}
