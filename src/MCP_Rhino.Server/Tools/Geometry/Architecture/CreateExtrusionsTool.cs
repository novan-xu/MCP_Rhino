using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreateExtrusionsTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreateExtrusionsTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create capped architectural extrusions from closed planar profiles in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreateExtrusions(
        string filePath,
        List<ExtrusionItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreateExtrusions(new CreateExtrusionsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<ExtrusionItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

