using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreatePlanarBrepsTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreatePlanarBrepsTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create planar Breps from closed planar loops in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreatePlanarBreps(
        string filePath,
        List<PlanarBrepItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreatePlanarBreps(new CreatePlanarBrepsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<PlanarBrepItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

