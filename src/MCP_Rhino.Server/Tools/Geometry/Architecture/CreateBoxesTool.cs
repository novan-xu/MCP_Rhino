using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreateBoxesTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreateBoxesTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create architectural box or box-extrusion solids in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreateBoxes(
        string filePath,
        List<BoxItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreateBoxes(new CreateBoxesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BoxItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

