using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateRoundedBoxesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateRoundedBoxesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch oriented rounded-box Brep primitives in the current live Rhino document on an existing layer. Use for soft rectangular massing such as cushions, cases, pads, and appliance bodies; created objects receive reference-image object-modeling metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateRoundedBoxes(
        string filePath,
        List<RoundedBoxItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateRoundedBoxesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<RoundedBoxItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
