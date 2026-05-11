using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry;

[McpServerToolType]
public sealed class CreateEllipsoidsTool
{
    private readonly GeometryCreationSkill _skill;

    public CreateEllipsoidsTool(GeometryCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create batch oriented ellipsoid Brep primitives in the current live Rhino document on an existing layer. Use for soft massing volumes from reference-image decomposition; created objects receive reference-image object-modeling metadata.")]
    public OperationResponse<GeneralPrimitiveCreationResponse> CreateEllipsoids(
        string filePath,
        List<EllipsoidItemRequest> items,
        GeometryCreationCommonOptions? common = null)
    {
        return _skill.Create(new CreateEllipsoidsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<EllipsoidItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions()
        });
    }
}
