using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewPurgeUnusedBlockDefinitionsTool
{
    private readonly BlockLifecycleSkill _skill;

    public PreviewPurgeUnusedBlockDefinitionsTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview purge of unused local Rhino block definitions without mutating the live document.")]
    public OperationResponse<BlockMutationPreviewResponse> PreviewPurgeUnusedBlockDefinitions(
        string filePath,
        List<string>? definitionNames = null,
        bool includeAllUnused = true,
        BlockPurgePolicy policy = BlockPurgePolicy.UnusedLocalOnly)
    {
        return _skill.PreviewPurge(new PreviewPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = definitionNames ?? new List<string>(),
            IncludeAllUnused = includeAllUnused,
            Policy = policy
        });
    }
}
