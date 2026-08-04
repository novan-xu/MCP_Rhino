using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateSurfacesTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateSurfacesTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch surface objects in the current live Rhino document on an existing layer.")]
    public OperationResponse<GeometryCreationResponse> CreateSurfaces(
        string filePath,
        List<SurfaceItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateSurfacesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SurfaceItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
