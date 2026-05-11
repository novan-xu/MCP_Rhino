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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview batch object edits in the current live Rhino document, including user text, layer, and display color changes, without mutating the document.")]
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
