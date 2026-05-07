using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateLinesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateLinesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("在 Rhino .3dm 文件中批量创建线对象。")]
    public OperationResponse<GeometryCreationResponse> CreateLines(
        string filePath,
        List<LineItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateLinesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<LineItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
