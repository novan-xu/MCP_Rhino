using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateToriTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateToriTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch oriented torus Brep primitives in the current live Rhino document on an existing layer. Use for rings, rims, loops, wheels, and gaskets in reference-image object modeling; created objects receive workflow metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateTori(
        string filePath,
        List<TorusItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateToriRequest
        {
            FilePath = filePath,
            Items = items ?? new List<TorusItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
