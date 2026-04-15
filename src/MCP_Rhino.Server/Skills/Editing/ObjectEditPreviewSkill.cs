using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.Editing;

public sealed class ObjectEditPreviewSkill
{
    private readonly ObjectSelectionSkill _objectSelectionSkill;
    private readonly RhinoObjectEditingService _editingService;

    public ObjectEditPreviewSkill(
        ObjectSelectionSkill objectSelectionSkill,
        RhinoObjectEditingService editingService)
    {
        _objectSelectionSkill = objectSelectionSkill;
        _editingService = editingService;
    }

    public OperationResponse<ObjectEditPreviewResponse> Preview(PreviewObjectEditsRequest request)
    {
        var selection = _objectSelectionSkill.Select(new FilterObjectsRequest
        {
            FilePath = request.FilePath,
            LayerQueries = request.LayerQueries,
            ConfirmedLayerFullPaths = request.ConfirmedLayerFullPaths,
            ObjectTypes = request.ObjectTypes,
            UserAttributeConditions = request.UserAttributeConditions,
            MatchMode = request.MatchMode,
            UserAttributeMatchMode = request.UserAttributeMatchMode
        });

        if (!selection.Success || selection.Data is null)
        {
            return OperationResponse<ObjectEditPreviewResponse>.Fail(selection.Message);
        }

        return _editingService.Preview(request, selection.Data);
    }
}