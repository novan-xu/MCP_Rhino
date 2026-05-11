using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreatePointsTool
{
    private readonly GeometryCreationSkill _skill;

    public CreatePointsTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch point objects in the current live Rhino document on an existing layer.")]
    public OperationResponse<GeometryCreationResponse> CreatePoints(
        string filePath,
        List<PointItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreatePointsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<PointItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
