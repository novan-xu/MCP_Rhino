using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateCurveOffsetsTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateCurveOffsetsTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create offset curves from existing curve object ids using an explicit offset plane and corner style in the current live Rhino document. Source curves are preserved.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateCurveOffsets(
        string filePath,
        List<CurveOffsetEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateCurveOffsets(new CreateCurveOffsetsRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveOffsetEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
