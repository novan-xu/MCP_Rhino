using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreateSlabsTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreateSlabsTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create slab solids from closed footprints in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreateSlabs(
        string filePath,
        List<SlabItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreateSlabs(new CreateSlabsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<SlabItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

