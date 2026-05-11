using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateSweepOneRailTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateSweepOneRailTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create one-rail sweep Breps from an existing rail curve id and profile curve ids in the current live Rhino document. Source curves are preserved.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateSweepOneRail(
        string filePath,
        List<SweepOneRailEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateSweepOneRail(new CreateSweepOneRailRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<SweepOneRailEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
