using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class CheckContinuityInLiveTool
{
    private readonly RhinoGeometryCurvatureService _service;

    public CheckContinuityInLiveTool(RhinoGeometryCurvatureService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Check live G0/G1/G2 continuity between curves or between surface/brep edges in the active Rhino document. ObjectId only.")]
    public OperationResponse<CheckContinuityInLiveResponse> CheckContinuityInLive(string filePath, List<ContinuityCheckEntryRequest> entries)
    {
        return _service.CheckContinuityInLive(new CheckContinuityInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ContinuityCheckEntryRequest>()
        });
    }
}
