using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateNurbsCurvesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateNurbsCurvesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch NURBS/control-point curve primitives in the current live Rhino document on an existing layer. Degree must be 1 through 11 with at least Degree + 1 control points.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateNurbsCurves(
        string filePath,
        List<NurbsCurveItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateNurbsCurvesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<NurbsCurveItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
