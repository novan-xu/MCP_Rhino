extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoSelectionOperator : ILiveRhinoSelectionOperator
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveRhinoSelectionOperator(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<SelectedObjectsResponse> GetSelectedObjects(string filePath)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            Dictionary<int, LiveLayer> layerLookup = LiveRhinoObjectInfoMapper.BuildLayerLookup(document);
            List<RhinoObjectInfo> selectedObjects = document.Objects.GetSelectedObjects(true, true)
                .Where(rhinoObject => !rhinoObject.IsDeleted)
                .Select(rhinoObject => LiveRhinoObjectInfoMapper.BuildObjectInfo(rhinoObject, layerLookup))
                .ToList();

            return OperationResponse<SelectedObjectsResponse>.Ok(new SelectedObjectsResponse
            {
                FilePath = document.Path,
                SelectedCount = selectedObjects.Count,
                Objects = selectedObjects
            }, $"Read {selectedObjects.Count} selected live objects.");
        });
    }

    public OperationResponse<SelectionMutationResponse> SelectObjects(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        RhinoSelectionMode selectionMode,
        int requestedObjectCount,
        IReadOnlyList<Guid> missingObjectIds)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            List<Guid> previousSelectedIds = document.Objects.GetSelectedObjects(true, true)
                .Where(rhinoObject => !rhinoObject.IsDeleted)
                .Select(rhinoObject => rhinoObject.Id)
                .Distinct()
                .ToList();

            List<Guid> distinctIds = objectIds
                .Where(objectId => objectId != Guid.Empty)
                .Distinct()
                .ToList();

            List<Guid> matchedIds = new(distinctIds.Count);
            List<Guid> missingIds = new(missingObjectIds);
            foreach (Guid objectId in distinctIds)
            {
                RhinoObject? rhinoObject = document.Objects.FindId(objectId);
                if (rhinoObject is null || rhinoObject.IsDeleted)
                {
                    missingIds.Add(objectId);
                    continue;
                }

                matchedIds.Add(objectId);
            }

            switch (selectionMode)
            {
                case RhinoSelectionMode.Clear:
                    document.Objects.UnselectAll();
                    break;
                case RhinoSelectionMode.Replace:
                    document.Objects.UnselectAll();
                    if (matchedIds.Count > 0)
                    {
                        document.Objects.Select(matchedIds);
                    }
                    break;
                case RhinoSelectionMode.Add:
                    if (matchedIds.Count > 0)
                    {
                        document.Objects.Select(matchedIds);
                    }
                    break;
                case RhinoSelectionMode.Remove:
                    if (matchedIds.Count > 0)
                    {
                        document.Objects.Select(matchedIds, false);
                    }
                    break;
                default:
                    return OperationResponse<SelectionMutationResponse>.Fail($"Unsupported selection mode: {selectionMode}");
            }

            document.Views.Redraw();

            Dictionary<int, LiveLayer> layerLookup = LiveRhinoObjectInfoMapper.BuildLayerLookup(document);
            List<RhinoObjectInfo> selectedObjects = document.Objects.GetSelectedObjects(true, true)
                .Where(rhinoObject => !rhinoObject.IsDeleted)
                .Select(rhinoObject => LiveRhinoObjectInfoMapper.BuildObjectInfo(rhinoObject, layerLookup))
                .ToList();

            var warnings = new List<ObjectEditWarning>();
            if (missingIds.Count > 0)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "SELECTION_OBJECT_IDS_NOT_FOUND",
                    Message = $"{missingIds.Count} requested ObjectIds did not resolve in the live document."
                });
            }

            return OperationResponse<SelectionMutationResponse>.Ok(new SelectionMutationResponse
            {
                FilePath = document.Path,
                SelectionMode = selectionMode,
                RequestedObjectCount = requestedObjectCount,
                MatchedObjectCount = matchedIds.Count,
                PreviousSelectedCount = previousSelectedIds.Count,
                CurrentSelectedCount = selectedObjects.Count,
                MissingObjectIds = missingIds.Distinct().ToList(),
                SelectedObjects = selectedObjects,
                Warnings = warnings
            }, $"Selection updated with mode {selectionMode}.");
        });
    }
}
