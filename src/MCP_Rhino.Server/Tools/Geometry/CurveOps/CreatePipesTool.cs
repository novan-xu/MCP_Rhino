using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreatePipesTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreatePipesTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create pipe Breps along existing curve object ids in the current live Rhino document. Radius and cap style are typed inputs; source curves are preserved.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreatePipes(
        string filePath,
        List<PipeEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreatePipes(new CreatePipesRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<PipeEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
