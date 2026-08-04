using System.ComponentModel;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Skills.Editing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class ApplyBulkObjectAttributeRecipeTool
{
    private readonly ObjectAttributeRecipeSkill _recipeSkill;

    public ApplyBulkObjectAttributeRecipeTool(ObjectAttributeRecipeSkill recipeSkill)
    {
        _recipeSkill = recipeSkill;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Apply compact bulk object attribute recipes in the current live Rhino document. Selects objects by layer, type, and/or user attributes, resolves recipe templates server-side, and applies user text, layer, display color, and object name changes in one Rhino undo record without external file access.")]
    public OperationResponse<ObjectEditExecutionResponse> ApplyBulkObjectAttributeRecipe(
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
        return _recipeSkill.Apply(new ApplyBulkObjectAttributeRecipeRequest
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
