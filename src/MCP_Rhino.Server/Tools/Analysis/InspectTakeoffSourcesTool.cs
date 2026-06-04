using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class InspectTakeoffSourcesTool
{
    private readonly RhinoTakeoffScheduleService _service;

    public InspectTakeoffSourcesTool(RhinoTakeoffScheduleService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Inspect live Rhino objects for flexible take-off scheduling. Returns scoped object counts, layer/type summaries, user text keys with sample values, supported quantity metrics, and clarification candidates; does not write files or mutate Rhino.")]
    public OperationResponse<TakeoffSourcesInspectionResponse> InspectTakeoffSources(
        string filePath,
        TakeoffScopeRequest? scope = null,
        int sampleValueCount = 5)
    {
        return _service.InspectSources(new InspectTakeoffSourcesRequest
        {
            FilePath = filePath,
            Scope = scope,
            SampleValueCount = sampleValueCount
        });
    }
}
