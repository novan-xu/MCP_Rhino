using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyTransformBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public ApplyTransformBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Transform Rhino block instance objects in the live document.")]
    public OperationResponse<BlockMutationApplyResponse> ApplyTransformBlockInstances(
        string filePath,
        List<BlockInstanceTransformRequest> items)
    {
        return _skill.ApplyTransform(new ApplyTransformBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockInstanceTransformRequest>()
        });
    }
}
