using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoSelectionService
{
    private readonly ILiveRhinoSelectionOperator _selectionOperator;
    private readonly RhinoObjectFilterService _filterService;

    public RhinoSelectionService(
        ILiveRhinoSelectionOperator selectionOperator,
        RhinoObjectFilterService filterService)
    {
        _selectionOperator = selectionOperator;
        _filterService = filterService;
    }

    public OperationResponse<SelectedObjectsResponse> GetSelectedObjects(GetSelectedObjectsInLiveRequest request)
    {
        return _selectionOperator.GetSelectedObjects(request.FilePath);
    }

    public OperationResponse<SelectionMutationResponse> SelectObjects(SelectObjectsInLiveRequest request)
    {
        List<Guid> requestedIds = (request.ObjectIds ?? new List<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToList();

        if (request.Filter is not null && HasAnyFilterCriteria(request.Filter))
        {
            request.Filter.FilePath = request.FilePath;
            OperationResponse<RhinoObjectFilterResult> filtered = _filterService.FilterInLive(request.Filter);
            if (!filtered.Success || filtered.Data is null)
            {
                return OperationResponse<SelectionMutationResponse>.Fail(filtered.Message);
            }

            requestedIds.AddRange(filtered.Data.Objects.Select(item => item.ObjectId));
            requestedIds = requestedIds.Distinct().ToList();
        }

        if (request.SelectionMode != Domain.Enums.RhinoSelectionMode.Clear && requestedIds.Count == 0)
        {
            return OperationResponse<SelectionMutationResponse>.Fail("At least one ObjectId or filter criterion is required unless selectionMode is Clear.");
        }

        return _selectionOperator.SelectObjects(
            request.FilePath,
            requestedIds,
            request.SelectionMode,
            requestedIds.Count,
            Array.Empty<Guid>());
    }

    private static bool HasAnyFilterCriteria(FilterObjectsRequest request)
    {
        return request.LayerQueries.Count > 0
            || request.ConfirmedLayerFullPaths.Count > 0
            || request.ObjectTypes.Count > 0
            || request.UserAttributeConditions.Count > 0;
    }
}
