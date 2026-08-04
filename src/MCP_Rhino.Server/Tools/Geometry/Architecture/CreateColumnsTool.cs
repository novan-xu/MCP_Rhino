using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Architecture;

[McpServerToolType]
public sealed class CreateColumnsTool
{
    private readonly ArchitecturalPrimitiveCreationSkill _skill;

    public CreateColumnsTool(ArchitecturalPrimitiveCreationSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create rectangular or circular column solids in the live Rhino document.")]
    public OperationResponse<ArchitecturalCreationResponse> CreateColumns(
        string filePath,
        List<ColumnItemRequest> items,
        GeometryCreationCommonOptions? common = null,
        ArchitecturalMetadataRequest? metadata = null,
        bool autoCreateLayers = false)
    {
        return _skill.CreateColumns(new CreateColumnsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<ColumnItemRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            Metadata = metadata ?? new ArchitecturalMetadataRequest(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}

