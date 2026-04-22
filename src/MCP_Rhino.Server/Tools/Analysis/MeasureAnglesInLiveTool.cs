using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class MeasureAnglesInLiveTool
{
    private readonly RhinoGeometryMetricsService _service;

    public MeasureAnglesInLiveTool(RhinoGeometryMetricsService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Measure live angles using three points, two vectors, or two curve tangents. Supports temporary geometry specs where applicable.")]
    public OperationResponse<MeasureAnglesInLiveResponse> MeasureAnglesInLive(string filePath, List<MeasureAngleEntryRequest> entries)
    {
        return _service.MeasureAnglesInLive(new MeasureAnglesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<MeasureAngleEntryRequest>()
        });
    }
}
