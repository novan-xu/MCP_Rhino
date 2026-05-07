using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetContourCurvesInLiveTool
{
    private readonly RhinoGeometryIntersectionService _service;

    public GetContourCurvesInLiveTool(RhinoGeometryIntersectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Create live contour-curve previews from surfaces or breps in the active Rhino document. ObjectId only.")]
    public OperationResponse<GetContourCurvesInLiveResponse> GetContourCurvesInLive(string filePath, List<GeometryContourEntryRequest> entries)
    {
        return _service.GetContourCurvesInLive(new GetContourCurvesInLiveRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<GeometryContourEntryRequest>()
        });
    }
}
