using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateEllipsesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateEllipsesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch ellipse curve primitives in the current live Rhino document on an existing layer. This adds objects only and validates radii plus plane orientation.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateEllipses(
        string filePath,
        List<EllipseItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateEllipsesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<EllipseItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
