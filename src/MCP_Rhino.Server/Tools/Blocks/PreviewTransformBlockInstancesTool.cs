using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewTransformBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public PreviewTransformBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview Rhino block instance transforms without mutating the live document.")]
    public OperationResponse<BlockMutationPreviewResponse> PreviewTransformBlockInstances(
        string filePath,
        List<BlockInstanceTransformRequest> items)
    {
        return _skill.PreviewTransform(new PreviewTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockInstanceTransformRequest>()
        });
    }
}
