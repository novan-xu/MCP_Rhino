using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ApplyInsertBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public ApplyInsertBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Insert Rhino block instances by definition name.")]
    public OperationResponse<BlockMutationApplyResponse> ApplyInsertBlockInstances(
        string filePath,
        List<BlockInstancePlacementRequest> items,
        GeometryCreationCommonOptions? common = null,
        bool autoCreateLayers = false)
    {
        return _skill.ApplyInsert(new ApplyInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockInstancePlacementRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}
