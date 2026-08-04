using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyPurgeUnusedBlockDefinitionsTool
{
    private readonly BlockLifecycleSkill _skill;

    public ApplyPurgeUnusedBlockDefinitionsTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Purge unused local Rhino block definitions from the live document.")]
    public OperationResponse<BlockMutationApplyResponse> ApplyPurgeUnusedBlockDefinitions(
        string filePath,
        List<string>? definitionNames = null,
        bool includeAllUnused = true,
        BlockPurgePolicy policy = BlockPurgePolicy.UnusedLocalOnly)
    {
        return _skill.ApplyPurge(new ApplyPurgeUnusedBlockDefinitionsRequest
        {
            FilePath = filePath,
            DefinitionNames = definitionNames ?? new List<string>(),
            IncludeAllUnused = includeAllUnused,
            Policy = policy
        });
    }
}
