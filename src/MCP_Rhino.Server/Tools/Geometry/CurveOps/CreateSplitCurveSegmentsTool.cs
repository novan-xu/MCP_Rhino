using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateSplitCurveSegmentsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateSplitCurveSegmentsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create split curve segment objects from existing curve object ids while preserving source curves. Use ReplaceSplitCurves only when source deletion is explicitly intended.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateSplitCurveSegments(
        string filePath,
        List<CurveSplitEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateSplitCurveSegments(new CreateSplitCurveSegmentsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveSplitEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
