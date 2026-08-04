using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.Modeling;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class PreviewInsertBlockInstancesTool
{
    private readonly BlockLifecycleSkill _skill;

    public PreviewInsertBlockInstancesTool(BlockLifecycleSkill skill)
    {
        _skill = skill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview Rhino block instance insertion without mutating the live document.")]
    public OperationResponse<BlockMutationPreviewResponse> PreviewInsertBlockInstances(
        string filePath,
        List<BlockInstancePlacementRequest> items,
        GeometryCreationCommonOptions? common = null,
        bool autoCreateLayers = false)
    {
        return _skill.PreviewInsert(new PreviewInsertBlockInstancesRequest
        {
            FilePath = filePath,
            Items = items ?? new List<BlockInstancePlacementRequest>(),
            Common = common ?? new GeometryCreationCommonOptions(),
            AutoCreateLayers = autoCreateLayers
        });
    }
}
