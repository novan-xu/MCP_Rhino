using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetCurvatureSamplesInLiveTool
{
    private readonly RhinoGeometryCurvatureService _service;

    public GetCurvatureSamplesInLiveTool(RhinoGeometryCurvatureService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Sample live curve or surface curvature from the active Rhino document. ObjectId only. Supports EvenByCount, EvenByLength, AtParameters, and AtUVList.")]
    public OperationResponse<GetCurvatureSamplesInLiveResponse> GetCurvatureSamplesInLive(string filePath, List<CurvatureSampleEntryRequest> entries)
    {
        return _service.GetCurvatureSamplesInLive(new GetCurvatureSamplesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurvatureSampleEntryRequest>()
        });
    }
}
