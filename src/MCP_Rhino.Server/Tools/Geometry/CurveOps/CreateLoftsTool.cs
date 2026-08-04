using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateLoftsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateLoftsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create loft Breps from ordered existing curve object ids in the current live Rhino document. This adds new geometry on an existing layer and keeps source curves.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateLofts(
        string filePath,
        List<LoftEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateLofts(new CreateLoftsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<LoftEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
