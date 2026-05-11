using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyExplodeBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public ApplyExplodeBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Explode Rhino block instances into child geometry in the live document.")]
    public OperationResponse<BlockMutationApplyResponse> ApplyExplodeBlockInstances(
        string filePath,
        List<BlockExplodeInstanceRequest> items)
    {
        return _skill.ApplyExplode(new ApplyExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockExplodeInstanceRequest>()
        });
    }
}
