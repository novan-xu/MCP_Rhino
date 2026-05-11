using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreatePolylinesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreatePolylinesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch polyline curve primitives in the current live Rhino document on an existing layer. Closed polylines require at least three supplied points.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreatePolylines(
        string filePath,
        List<PolylineItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreatePolylinesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<PolylineItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
