using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreatePipesFromPointsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreatePipesFromPointsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create pipe Breps directly from ordered rail point arrays in the current live Rhino document. Use for real rods, rails, seams, cords, and rounded product edges when no source curve object exists; do not use for shadow lines, weave texture, wood grain, or lighting marks.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreatePipesFromPoints(
        string filePath,
        List<PipeFromPointsEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreatePipesFromPoints(new CreatePipesFromPointsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<PipeFromPointsEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
