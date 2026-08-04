using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateConesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateConesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch cone Brep primitives in the current live Rhino document on an existing layer. Radius, height, and axis are validated before objects are added.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateCones(
        string filePath,
        List<ConeItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateConesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<ConeItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
