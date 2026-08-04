using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class CreateLoftsFromProfilesTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public CreateLoftsFromProfilesTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create loft Breps directly from ordered profile point arrays in the current live Rhino document. Use for sloped cushions, molded shells, and product forms when source curve objects do not already exist; this adds new geometry and keeps material, texture, and shadows out of geometry.")]
    public OperationResponse<CurveDerivedGeometryResponse> CreateLoftsFromProfiles(
        string filePath,
        List<LoftFromProfilesEntryRequest> entries,
        GeometryCreationCommonOptions? common = null)
    {
        return _service.CreateLoftsFromProfiles(new CreateLoftsFromProfilesRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<LoftFromProfilesEntryRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
