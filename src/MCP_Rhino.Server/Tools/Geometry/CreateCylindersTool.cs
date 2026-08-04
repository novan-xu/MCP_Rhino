using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateCylindersTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateCylindersTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch cylinder Brep primitives in the current live Rhino document on an existing layer. Radius, height, axis, and caps are explicit typed inputs.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateCylinders(
        string filePath,
        List<CylinderItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateCylindersRequest
        {
            FilePath = filePath,
            Items = items ?? new List<CylinderItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
