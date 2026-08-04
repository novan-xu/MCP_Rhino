using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewExplodeBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public PreviewExplodeBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview Rhino block instance explode impact without mutating the live document.")]
    public OperationResponse<BlockMutationPreviewResponse> PreviewExplodeBlockInstances(
        string filePath,
        List<BlockExplodeInstanceRequest> items)
    {
        return _skill.PreviewExplode(new PreviewExplodeBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockExplodeInstanceRequest>()
        });
    }
}
