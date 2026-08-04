using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateTaperedBoxesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateTaperedBoxesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch tapered box-like mesh primitives between explicit start and end points in the current live Rhino document on an existing layer. Use for splayed furniture legs, A-frame members, wedge arms, sloped supports, and rails whose start/end section sizes differ; do not use for shadows, woven texture, grain, or lighting cues.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateTaperedBoxes(
        string filePath,
        List<TaperedBoxItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateTaperedBoxesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<TaperedBoxItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
