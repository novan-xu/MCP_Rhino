using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreateBeamsTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreateBeamsTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create rectangular or circular beam solids along baselines in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreateBeams(
        string filePath,
        List<BeamItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreateBeams(new CreateBeamsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BeamItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

