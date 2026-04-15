using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Editing;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class PreviewObjectEditsTool
{
    private readonly ObjectEditPreviewSkill _previewSkill;

    public PreviewObjectEditsTool(ObjectEditPreviewSkill previewSkill)
    {
        _previewSkill = previewSkill;
    }

    [McpServerTool]
    [Description("预览 Rhino 物体批量编辑。支持 user text、图层、显示颜色修改，并复用现有筛查条件。")]
    public OperationResponse<ObjectEditPreviewResponse> PreviewObjectEdits(
        string filePath,
        List<ObjectEditOperationRequest> operations,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _previewSkill.Preview(new PreviewObjectEditsRequest
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