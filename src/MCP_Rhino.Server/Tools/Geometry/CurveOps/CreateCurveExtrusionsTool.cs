using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateCurveExtrusionsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateCurveExtrusionsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create extrusion surfaces or capped Breps from existing curve object ids and explicit vectors in the current live Rhino document. Source curves are preserved.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveExtrusions(
        string filePath,
        List<CurveExtrusionEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateCurveExtrusions(new CreateCurveExtrusionsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveExtrusionEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
