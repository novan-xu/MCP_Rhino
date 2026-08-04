using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Editing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class PreviewBulkObjectAttributeRecipeTool
{
    private readonly ObjectAttributeRecipeSkill _recipeSkill;

    public PreviewBulkObjectAttributeRecipeTool(ObjectAttributeRecipeSkill recipeSkill)
    {
        _recipeSkill = recipeSkill;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview compact bulk object attribute recipes in the current live Rhino document without mutating it. Selects objects by layer, type, and/or user attributes, then resolves user text, layer, display color, and object name recipe changes server-side to avoid large per-object payloads.")]
    public OperationResponse<ObjectEditPreviewResponse> PreviewBulkObjectAttributeRecipe(
        string filePath,
        List<ObjectAttributeUserTextRecipeRequest>? userTextWrites = null,
        List<string>? removeUserTextKeys = null,
        string? targetLayerFullPath = null,
        ObjectColorRequest? displayColor = null,
        string? objectNameTemplate = null,
        List<string>? layerQueries = null,
        List<string>? confirmedLayerFullPaths = null,
        List<string>? objectTypes = null,
        List<UserAttributeConditionRequest>? userAttributeConditions = null,
        FilterMatchMode matchMode = FilterMatchMode.All,
        FilterMatchMode userAttributeMatchMode = FilterMatchMode.All)
    {
        return _recipeSkill.Preview(new PreviewBulkObjectAttributeRecipeRequest
        {
            FilePath = filePath,
            UserTextWrites = userTextWrites ?? new List<ObjectAttributeUserTextRecipeRequest>(),
            RemoveUserTextKeys = removeUserTextKeys ?? new List<string>(),
            TargetLayerFullPath = targetLayerFullPath ?? string.Empty,
            DisplayColor = displayColor,
            ObjectNameTemplate = objectNameTemplate ?? string.Empty,
            LayerQueries = layerQueries ?? new List<string>(),
            ConfirmedLayerFullPaths = confirmedLayerFullPaths ?? new List<string>(),
            ObjectTypes = objectTypes ?? new List<string>(),
            UserAttributeConditions = userAttributeConditions ?? new List<UserAttributeConditionRequest>(),
            MatchMode = matchMode,
            UserAttributeMatchMode = userAttributeMatchMode
        });
    }
}
