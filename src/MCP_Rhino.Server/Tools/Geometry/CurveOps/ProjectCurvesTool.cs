using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class ProjectCurvesTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public ProjectCurvesTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Project existing curve object ids onto live Brep, surface, extrusion, or mesh target object ids along an explicit direction. This creates projected curves and preserves sources.")]
    public OperationResponse<CurveDerivedGeometryResponse> ProjectCurves(
        string filePath,
        List<CurveProjectionEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.ProjectCurves(new ProjectCurvesRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveProjectionEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
