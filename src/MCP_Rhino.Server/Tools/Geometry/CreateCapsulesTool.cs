using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateCapsulesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateCapsulesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch capsule Brep primitives along explicit start-end rails in the current live Rhino document on an existing layer. Use for rods, rolls, handles, and pill-shaped soft parts; created objects receive reference-image object-modeling metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateCapsules(
        string filePath,
        List<CapsuleItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateCapsulesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<CapsuleItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
