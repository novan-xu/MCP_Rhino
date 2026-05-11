using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateProfileExtrusionsFromPointsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateProfileExtrusionsFromPointsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create capped or uncapped extrusions directly from closed profile point arrays in the current live Rhino document. Use for furniture side plates, brackets, A-frame panels, and product profiles when no source curve object exists; this adds new geometry and does not model material, shadow, or texture cues.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateProfileExtrusionsFromPoints(
        string filePath,
        List<ProfileExtrusionFromPointsEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateProfileExtrusionsFromPoints(new CreateProfileExtrusionsFromPointsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ProfileExtrusionFromPointsEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
