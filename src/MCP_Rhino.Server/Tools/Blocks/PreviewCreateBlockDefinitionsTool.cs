using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewCreateBlockDefinitionsTool
{
    private readonly BlockLifecycleSkill _skill;

    public PreviewCreateBlockDefinitionsTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview local Rhino block definition creation without mutating the live document.")]
    public OperationResponse<BlockMutationPreviewResponse> PreviewCreateBlockDefinitions(
        string filePath,
        List<BlockDefinitionSourceItemRequest> items)
    {
        return _skill.PreviewCreate(new PreviewCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockDefinitionSourceItemRequest>()
        });
    }
}
