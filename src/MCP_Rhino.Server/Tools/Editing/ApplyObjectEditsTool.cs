using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Editing;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class ApplyObjectEditsTool
{
    private readonly ObjectEditApplySkill _applySkill;

    public ApplyObjectEditsTool(ObjectEditApplySkill applySkill)
    {
        _applySkill = applySkill;
    }

    [McpServerTool]
    [Description("执行 Rhino 物体批量编辑。支持 user text、图层、显示颜色修改，并直接覆盖原文件。")]
    public OperationResponse<ObjectEditExecutionResponse> ApplyObjectEdits(
        string filePath,
        List<ObjectEditOperationRequest> operations,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _applySkill.Apply(new ApplyObjectEditsRequest
        {
            FilePath = filePath,
            Operations = operations ?? new List<ObjectEditOperationRequest>(),
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}