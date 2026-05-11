using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Editing;

public sealed class ObjectAttributeRecipeSkill
{
    private readonly LiveObjectSelectionSkill _objectSelectionSkill;
    private readonly RhinoObjectAttributeRecipeService _recipeService;

    public ObjectAttributeRecipeSkill(
        LiveObjectSelectionSkill objectSelectionSkill,
        RhinoObjectAttributeRecipeService recipeService)
    {
        _objectSelectionSkill = objectSelectionSkill;
        _recipeService = recipeService;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(PreviewBulkObjectAttributeRecipeRequest request)
    {
        var selection = _objectSelectionSkill.Select(CreateSelectionRequest(request));
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail(selection.Message);
        }

        return _recipeService.Preview(request, selection.Data);
    }

    public OperationResponse<ObjectEditExecutionResponse> Apply(ApplyBulkObjectAttributeRecipeRequest request)
    {
        var selection = _objectSelectionSkill.Select(CreateSelectionRequest(request));
        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<ObjectEditExecutionResponse>.Fail(selection.Message);
        }

        return _recipeService.Apply(request, selection.Data);
    }

    private static FilterObjectsRequest CreateSelectionRequest(ObjectAttributeRecipeRequestBase request)
    {
        return new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = request.LayerQueries,
            ConfirmedLayerFullPaths = request.ConfirmedLayerFullPaths,
            ObjectTypes = request.ObjectTypes,
            UserAttributeConditions = request.UserAttributeConditions,
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        };
    }
}
