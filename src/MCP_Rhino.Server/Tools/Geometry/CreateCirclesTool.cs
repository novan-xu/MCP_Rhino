using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateCirclesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateCirclesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch circle curve primitives in the current live Rhino document on an existing layer. This adds objects only and does not read or write external files.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateCircles(
        string filePath,
        List<CircleItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateCirclesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<CircleItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
