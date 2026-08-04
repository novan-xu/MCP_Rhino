using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateSpheresTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateSpheresTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch sphere Brep primitives in the current live Rhino document on an existing layer. This generic primitive path adds objects without architectural metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateSpheres(
        string filePath,
        List<SphereItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateSpheresRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SphereItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
