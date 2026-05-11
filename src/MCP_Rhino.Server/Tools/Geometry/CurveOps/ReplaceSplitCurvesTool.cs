using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class ReplaceSplitCurvesTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public ReplaceSplitCurvesTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Replace existing curve object ids with newly created split segment objects in the current live Rhino document. This deletes source curves after segment creation.")]
    public OperationResponse<CurveDerivedGeometryResponse> ReplaceSplitCurves(
        string filePath,
        List<CurveSplitEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.ReplaceSplitCurves(new ReplaceSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveSplitEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
