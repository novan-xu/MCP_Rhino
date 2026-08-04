using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyCreateBlockDefinitionsTool
{
    private readonly BlockLifecycleSkill _skill;

    public ApplyCreateBlockDefinitionsTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Create local Rhino block definitions from existing live document object ids.")]
    public OperationResponse<BlockMutationApplyResponse> ApplyCreateBlockDefinitions(
        string filePath,
        List<BlockDefinitionSourceItemRequest> items)
    {
        return _skill.ApplyCreate(new ApplyCreateBlockDefinitionsRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockDefinitionSourceItemRequest>()
        });
    }
}
