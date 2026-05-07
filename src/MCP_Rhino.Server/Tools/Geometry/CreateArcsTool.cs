using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateArcsTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateArcsTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("在 Rhino .3dm 文件中批量创建圆弧对象。")]
    public OperationResponse<GeometryCreationResponse> CreateArcs(
        string filePath,
        List<ArcItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateArcsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<ArcItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
