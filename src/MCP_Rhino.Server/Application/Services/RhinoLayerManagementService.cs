extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;
using Rhino.FileIO;
using File3dmLayer = Rhino.DocObjects.Layer;
using File3dmLinetype = Rhino.DocObjects.Linetype;
using File3dmMaterial = Rhino.DocObjects.Material;
using LiveLayer = rhinocommon::Rhino.DocObjects.Layer;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoLayerManagementService
{
    private readonly IRhinoDocumentRepository _repository;
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public RhinoLayerManagementService(
        IRhinoDocumentRepository repository,
        ILiveRhinoDocumentAccessor documentAccessor)
    {
        _repository = repository;
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<LayerReadResponse> Get(GetLayersRequest request)
    {
        if (!_repository.Exists(request.FilePath))
        {
            return OperationResponse<LayerReadResponse>.Fail($"File was not found: {request.FilePath}");
        }

        try
        {
            using var model = _repository.Read(request.FilePath);

            Dictionary<int, int> objectCounts = BuildOfflineObjectCounts(model);
            Dictionary<int, string> linetypeNames = BuildOfflineLinetypeNames(model);
            Dictionary<int, string> materialNames = BuildOfflineMaterialNames(model);
            Dictionary<Guid, string> layerPathsById = model.AllLayers
                .Where(layer => !layer.IsDeleted)
                .ToDictionary(layer => layer.Id, layer => layer.FullPath);

            List<RhinoLayerDetail> entries = model.AllLayers
                .Where(layer => !layer.IsDeleted)
                .Select(layer => BuildOfflineDetail(layer, objectCounts, linetypeNames, materialNames, layerPathsById))
                .OrderBy(layer => layer.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var response = new LayerReadResponse
            {
                FilePath = request.FilePath,
                TotalCount = entries.Count,
                Warnings = CreateOfflineWarnings(request.FilePath),
                Entries = entries
            };

            return OperationResponse<LayerReadResponse>.Ok(response, "Layers read completed.");
        }
        catch (Exception ex)
        {
            return OperationResponse<LayerReadResponse>.Fail($"Layer read failed: {ex.Message}");
        }
    }

    public OperationResponse<LayerMutationResponse> Create(CreateLayersRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<LayerMutationResponse>.Fail("At least one layer creation entry is required.");
        }

        OperationResponse<List<PreparedCreateEntry>> prepared = PrepareCreateEntries(request.Entries);
        if (!prepared.Success || prepared.Data is null)
        {
            return OperationResponse<LayerMutationResponse>.Fail(prepared.Message);
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: CreateLayers", document =>
        {
            var results = new List<LayerMutationResultResponse>(prepared.Data.Count);
            bool mutated = false;
            HashSet<string> existingBefore = GetLiveLayerPaths(document);

            foreach (PreparedCreateEntry entry in prepared.Data.OrderBy(item => item.Depth).ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(entry.Request.LinetypeName))
                    {
                        entry.ResolvedLinetypeIndex = FindLiveLinetypeIndex(document, entry.Request.LinetypeName);
                    }

                    if (!string.IsNullOrWhiteSpace(entry.Request.RenderMaterialName))
                    {
                        entry.ResolvedMaterialIndex = FindLiveMaterialIndex(document, entry.Request.RenderMaterialName);
                    }

                    if (document.Layers.FindByFullPath(entry.FullPath, -1) >= 0)
                    {
                        results.Add(FailResult(entry.FullPath, $"Layer already exists: {entry.FullPath}"));
                        continue;
                    }

                    if (entry.ResolvedLinetypeIndex is null && !string.IsNullOrWhiteSpace(entry.Request.LinetypeName))
                    {
                        results.Add(FailResult(entry.FullPath, $"Linetype not found: {entry.Request.LinetypeName}"));
                        continue;
                    }

                    if (entry.ResolvedMaterialIndex is null && !string.IsNullOrWhiteSpace(entry.Request.RenderMaterialName))
                    {
                        results.Add(FailResult(entry.FullPath, $"Render material not found: {entry.Request.RenderMaterialName}"));
                        continue;
                    }

                    int createdIndex = document.Layers.AddPath(entry.FullPath);
                    if (createdIndex < 0)
                    {
                        results.Add(FailResult(entry.FullPath, $"Failed to create layer: {entry.FullPath}"));
                        continue;
                    }

                    int layerIndex = document.Layers.FindByFullPath(entry.FullPath, -1);
                    if (layerIndex < 0)
                    {
                        results.Add(FailResult(entry.FullPath, $"Created layer could not be resolved: {entry.FullPath}"));
                        continue;
                    }

                    LiveLayer liveLayer = document.Layers[layerIndex];
                    ApplyCommonFields(liveLayer, entry.Request, entry.ResolvedLinetypeIndex, entry.ResolvedMaterialIndex);
                    if (!document.Layers.Modify(liveLayer, layerIndex, true))
                    {
                        results.Add(FailResult(entry.FullPath, $"Failed to apply layer settings: {entry.FullPath}"));
                        continue;
                    }

                    HashSet<string> existingAfter = GetLiveLayerPaths(document);
                    List<string> autoCreatedParents = existingAfter
                        .Except(existingBefore, StringComparer.OrdinalIgnoreCase)
                        .Where(path => !string.Equals(path, entry.FullPath, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    existingBefore = existingAfter;

                    string message = $"Created layer [{entry.FullPath}]";
                    if (autoCreatedParents.Count > 0)
                    {
                        message += $" (auto-created parent layers: {string.Join(", ", autoCreatedParents.Select(path => $"[{path}]"))})";
                    }

                    results.Add(SuccessResult(entry.FullPath, entry.FullPath, message));
                    mutated = true;
                }
                catch (Exception ex)
                {
                    results.Add(FailResult(entry.FullPath, ex.Message));
                }
            }

            if (mutated)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Ok(
                (mutated, CreateMutationResponse(request.FilePath, prepared.Data.Count, results)),
                "Layer creation completed.");
        });
    }

    public OperationResponse<LayerModificationPreviewResponse> PreviewModify(PreviewModifyLayersRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<LayerModificationPreviewResponse>.Fail("At least one layer modification entry is required.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<List<PreparedModifyEntry>> prepared = PrepareModifyEntries(document, request.Entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<LayerModificationPreviewResponse>.Fail(prepared.Message);
            }

            List<LayerSnapshot> snapshots = BuildLiveLayerSnapshots(document);
            var snapshotLookup = snapshots.ToDictionary(item => item.Id);
            List<LayerModificationImpactResponse> impacts = prepared.Data
                .Select(entry => BuildModificationImpact(entry, snapshotLookup))
                .ToList();

            return OperationResponse<LayerModificationPreviewResponse>.Ok(new LayerModificationPreviewResponse
            {
                FilePath = request.FilePath,
                RequestedCount = request.Entries.Count,
                Impacts = impacts
            }, "Layer modification preview generated.");
        });
    }

    public OperationResponse<LayerMutationResponse> Modify(ModifyLayersRequest request)
    {
        if (request.Entries.Count == 0)
        {
            return OperationResponse<LayerMutationResponse>.Fail("At least one layer modification entry is required.");
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: ModifyLayers", document =>
        {
            OperationResponse<List<PreparedModifyEntry>> prepared = PrepareModifyEntries(document, request.Entries);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<LayerMutationResultResponse>(prepared.Data.Count);
            bool mutated = false;

            foreach (PreparedModifyEntry entry in prepared.Data)
            {
                if (!entry.CanApply)
                {
                    results.Add(FailResult(entry.RequestedFullPath, entry.FailureMessage ?? "Layer modification entry is invalid."));
                    continue;
                }

                try
                {
                    int layerIndex = FindLiveLayerIndex(document, entry.TargetLayerId);
                    if (layerIndex < 0)
                    {
                        results.Add(FailResult(entry.RequestedFullPath, $"Layer not found: {entry.RequestedFullPath}"));
                        continue;
                    }

                    if (entry.FieldDiffs.Count == 0)
                    {
                        string currentFullPath = document.Layers[layerIndex].FullPath;
                        results.Add(SuccessResult(entry.RequestedFullPath, currentFullPath, "No changes applied.", changed: false));
                        continue;
                    }

                    LiveLayer liveLayer = document.Layers[layerIndex];
                    ApplyPreparedModification(liveLayer, entry);
                    if (!document.Layers.Modify(liveLayer, layerIndex, true))
                    {
                        results.Add(FailResult(entry.RequestedFullPath, $"Failed to modify layer: {entry.RequestedFullPath}"));
                        continue;
                    }

                    int updatedIndex = FindLiveLayerIndex(document, entry.TargetLayerId);
                    string resolvedFullPath = updatedIndex >= 0 ? document.Layers[updatedIndex].FullPath : entry.ResolvedNewFullPath!;
                    results.Add(SuccessResult(entry.RequestedFullPath, resolvedFullPath, $"Modified layer [{entry.RequestedFullPath}] -> [{resolvedFullPath}]"));
                    mutated = true;
                }
                catch (Exception ex)
                {
                    results.Add(FailResult(entry.RequestedFullPath, ex.Message));
                }
            }

            if (mutated)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Ok(
                (mutated, CreateMutationResponse(request.FilePath, request.Entries.Count, results)),
                "Layer modification completed.");
        });
    }

    public OperationResponse<LayerDeletionPreviewResponse> PreviewDelete(PreviewDeleteLayersRequest request)
    {
        return PreviewDeletion(request.FilePath, request.FullPaths, isPurgePreview: false, "Layer delete preview generated.");
    }

    public OperationResponse<LayerDeletionPreviewResponse> PreviewPurge(PreviewPurgeLayersRequest request)
    {
        return PreviewDeletion(request.FilePath, request.FullPaths, isPurgePreview: true, "Layer purge preview generated.");
    }

    public OperationResponse<LayerMutationResponse> Delete(DeleteLayersRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: DeleteLayers", document =>
        {
            OperationResponse<List<PreparedDeleteTarget>> prepared = PrepareDeleteTargets(document, request.FullPaths);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<LayerMutationResultResponse>(prepared.Data.Count);
            bool mutated = false;

            foreach (PreparedDeleteTarget target in prepared.Data.OrderByDescending(item => item.Depth).ThenBy(item => item.RequestedFullPath, StringComparer.OrdinalIgnoreCase))
            {
                if (!target.CanApply)
                {
                    results.Add(FailResult(target.RequestedFullPath, target.FailureMessage ?? "Layer delete entry is invalid."));
                    continue;
                }

                try
                {
                    if (target.CurrentLayerInSubtree)
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Cannot delete the current layer (or a subtree containing it): {target.RequestedFullPath}"));
                        continue;
                    }

                    if (target.ParentLayerId == Guid.Empty)
                    {
                        results.Add(FailResult(
                            target.RequestedFullPath,
                            $"Cannot delete root layer [{target.RequestedFullPath}] — root has no parent layer for objects to migrate to. Either move objects to another layer first and retry, or use PurgeLayers to remove the root along with its objects."));
                        continue;
                    }

                    int parentLayerIndex = FindLiveLayerIndex(document, target.ParentLayerId);
                    if (parentLayerIndex < 0)
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Parent layer was not found for delete target: {target.RequestedFullPath}"));
                        continue;
                    }

                    HashSet<Guid> subtreeIds = target.SubtreeLayerIds.ToHashSet();
                    List<Guid> objectIds = CollectCurrentObjectIds(document, subtreeIds);
                    int movedObjects = 0;

                    foreach (Guid objectId in objectIds)
                    {
                        RhinoObject? currentObject = document.Objects.FindId(objectId);
                        if (currentObject is null || currentObject.IsDeleted)
                        {
                            continue;
                        }

                        ObjectAttributes attributes = currentObject.Attributes.Duplicate();
                        attributes.LayerIndex = parentLayerIndex;
                        if (!document.Objects.ModifyAttributes(objectId, attributes, true))
                        {
                            throw new InvalidOperationException($"Failed to move object [{objectId}] before deleting layer [{target.RequestedFullPath}].");
                        }

                        movedObjects++;
                    }

                    List<int> deleteIndices = target.SubtreeLayerIds
                        .Select(layerId => FindLiveLayerIndex(document, layerId))
                        .Where(index => index >= 0)
                        .OrderByDescending(index => SafeGetLayerDepth(document, index))
                        .ToList();

                    bool deleted = true;
                    foreach (int deleteIndex in deleteIndices)
                    {
                        if (!document.Layers.Delete(deleteIndex, true))
                        {
                            deleted = false;
                            break;
                        }
                    }

                    if (!deleted)
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Failed to delete layer subtree: {target.RequestedFullPath}"));
                        continue;
                    }

                    results.Add(SuccessResult(
                        target.RequestedFullPath,
                        target.RequestedFullPath,
                        $"Deleted layer [{target.RequestedFullPath}], moved {movedObjects} objects to parent layer [{target.ParentFullPath}]"));
                    mutated = true;
                }
                catch (Exception ex)
                {
                    results.Add(FailResult(target.RequestedFullPath, ex.Message));
                }
            }

            if (mutated)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Ok(
                (mutated, CreateMutationResponse(request.FilePath, request.FullPaths.Count, results)),
                "Layer delete completed.");
        });
    }

    public OperationResponse<LayerMutationResponse> Purge(PurgeLayersRequest request)
    {
        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: PurgeLayers", document =>
        {
            OperationResponse<List<PreparedDeleteTarget>> prepared = PrepareDeleteTargets(document, request.FullPaths);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Fail(prepared.Message);
            }

            var results = new List<LayerMutationResultResponse>(prepared.Data.Count);
            bool mutated = false;

            foreach (PreparedDeleteTarget target in prepared.Data.OrderByDescending(item => item.Depth).ThenBy(item => item.RequestedFullPath, StringComparer.OrdinalIgnoreCase))
            {
                if (!target.CanApply)
                {
                    results.Add(FailResult(target.RequestedFullPath, target.FailureMessage ?? "Layer purge entry is invalid."));
                    continue;
                }

                try
                {
                    if (target.CurrentLayerInSubtree)
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Cannot purge the current layer (or a subtree containing it): {target.RequestedFullPath}"));
                        continue;
                    }

                    int layerIndex = FindLiveLayerIndex(document, target.TargetLayerId);
                    if (layerIndex < 0)
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Layer not found: {target.RequestedFullPath}"));
                        continue;
                    }

                    CurrentSubtreeImpact impact = BuildCurrentSubtreeImpact(document, target.TargetLayerId);
                    if (!document.Layers.Purge(layerIndex, true))
                    {
                        results.Add(FailResult(target.RequestedFullPath, $"Failed to purge layer subtree: {target.RequestedFullPath}"));
                        continue;
                    }

                    results.Add(SuccessResult(
                        target.RequestedFullPath,
                        target.RequestedFullPath,
                        $"Purged layer [{target.RequestedFullPath}], removed {impact.TotalObjectCount} objects across {impact.DescendantLayerCount} sub-layers"));
                    mutated = true;
                }
                catch (Exception ex)
                {
                    results.Add(FailResult(target.RequestedFullPath, ex.Message));
                }
            }

            if (mutated)
            {
                document.Views.Redraw();
            }

            return OperationResponse<(bool Mutated, LayerMutationResponse Result)>.Ok(
                (mutated, CreateMutationResponse(request.FilePath, request.FullPaths.Count, results)),
                "Layer purge completed.");
        });
    }

    private OperationResponse<LayerDeletionPreviewResponse> PreviewDeletion(
        string filePath,
        IReadOnlyList<string> fullPaths,
        bool isPurgePreview,
        string successMessage)
    {
        if (fullPaths.Count == 0)
        {
            return OperationResponse<LayerDeletionPreviewResponse>.Fail("At least one layer full path is required.");
        }

        return _documentAccessor.Execute(filePath, document =>
        {
            OperationResponse<List<PreparedDeleteTarget>> prepared = PrepareDeleteTargets(document, fullPaths);
            if (!prepared.Success || prepared.Data is null)
            {
                return OperationResponse<LayerDeletionPreviewResponse>.Fail(prepared.Message);
            }

            List<LayerDeletionImpactResponse> impacts = prepared.Data
                .OrderBy(item => item.OriginalOrder)
                .Select(target => BuildDeletionImpact(target, isPurgePreview))
                .ToList();

            return OperationResponse<LayerDeletionPreviewResponse>.Ok(new LayerDeletionPreviewResponse
            {
                FilePath = filePath,
                RequestedCount = fullPaths.Count,
                Impacts = impacts
            }, successMessage);
        });
    }

    private OperationResponse<List<PreparedCreateEntry>> PrepareCreateEntries(IReadOnlyList<LayerCreationEntryRequest> entries)
    {
        var prepared = new List<PreparedCreateEntry>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < entries.Count; i++)
        {
            LayerCreationEntryRequest request = entries[i];
            string fullPath;

            try
            {
                fullPath = NormalizeLayerPath(request.FullPath);
            }
            catch (Exception ex)
            {
                return OperationResponse<List<PreparedCreateEntry>>.Fail(ex.Message);
            }

            if (!seen.Add(fullPath))
            {
                return OperationResponse<List<PreparedCreateEntry>>.Fail($"Duplicate layer creation entry: {fullPath}");
            }

            prepared.Add(new PreparedCreateEntry
            {
                Request = request,
                FullPath = fullPath,
                Depth = GetLayerDepth(fullPath)
            });
        }

        return OperationResponse<List<PreparedCreateEntry>>.Ok(prepared);
    }

    private OperationResponse<List<PreparedModifyEntry>> PrepareModifyEntries(RhinoDoc document, IReadOnlyList<LayerModificationEntryRequest> entries)
    {
        List<LayerSnapshot> snapshots = BuildLiveLayerSnapshots(document);
        Dictionary<string, LayerSnapshot> snapshotByFullPath = snapshots.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);
        Dictionary<Guid, LayerSnapshot> snapshotById = snapshots.ToDictionary(item => item.Id);

        var prepared = new List<PreparedModifyEntry>(entries.Count);
        var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < entries.Count; i++)
        {
            LayerModificationEntryRequest request = entries[i];
            var preparedEntry = new PreparedModifyEntry
            {
                OriginalOrder = i,
                Request = request,
                RequestedFullPath = request.FullPath?.Trim() ?? string.Empty
            };

            string normalizedTarget;
            try
            {
                normalizedTarget = NormalizeLayerPath(request.FullPath ?? string.Empty);
                preparedEntry.RequestedFullPath = normalizedTarget;
            }
            catch (Exception ex)
            {
                preparedEntry.FailureMessage = ex.Message;
                prepared.Add(preparedEntry);
                continue;
            }

            if (!seenTargets.Add(normalizedTarget))
            {
                return OperationResponse<List<PreparedModifyEntry>>.Fail($"Duplicate layer modification target: {normalizedTarget}");
            }

            if (!snapshotByFullPath.TryGetValue(normalizedTarget, out LayerSnapshot? target))
            {
                preparedEntry.FailureMessage = $"Layer not found: {normalizedTarget}";
                prepared.Add(preparedEntry);
                continue;
            }

            preparedEntry.TargetLayerId = target.Id;
            preparedEntry.TargetSnapshot = target;

            if (request.ClearPlotColor && request.PlotColor is not null)
            {
                preparedEntry.FailureMessage = "PlotColor cannot be set and cleared in the same entry.";
                prepared.Add(preparedEntry);
                continue;
            }

            if (request.ClearPlotWeight && request.PlotWeight.HasValue)
            {
                preparedEntry.FailureMessage = "PlotWeight cannot be set and cleared in the same entry.";
                prepared.Add(preparedEntry);
                continue;
            }

            if (request.ClearLinetype && !string.IsNullOrWhiteSpace(request.LinetypeName))
            {
                preparedEntry.FailureMessage = "Linetype cannot be set and cleared in the same entry.";
                prepared.Add(preparedEntry);
                continue;
            }

            if (request.ClearRenderMaterial && !string.IsNullOrWhiteSpace(request.RenderMaterialName))
            {
                preparedEntry.FailureMessage = "Render material cannot be set and cleared in the same entry.";
                prepared.Add(preparedEntry);
                continue;
            }

            string resolvedName = target.LayerName;
            if (request.NewName is not null)
            {
                try
                {
                    resolvedName = NormalizeLayerName(request.NewName);
                }
                catch (Exception ex)
                {
                    preparedEntry.FailureMessage = ex.Message;
                    prepared.Add(preparedEntry);
                    continue;
                }
            }

            Guid resolvedParentId = target.ParentLayerId;
            string? resolvedParentFullPath = target.ParentFullPath;
            if (request.NewParentFullPath is not null)
            {
                if (string.IsNullOrWhiteSpace(request.NewParentFullPath))
                {
                    resolvedParentId = Guid.Empty;
                    resolvedParentFullPath = null;
                }
                else
                {
                    string normalizedParent;
                    try
                    {
                        normalizedParent = NormalizeLayerPath(request.NewParentFullPath);
                    }
                    catch (Exception ex)
                    {
                        preparedEntry.FailureMessage = ex.Message;
                        prepared.Add(preparedEntry);
                        continue;
                    }

                    if (!snapshotByFullPath.TryGetValue(normalizedParent, out LayerSnapshot? parentSnapshot))
                    {
                        preparedEntry.FailureMessage = $"Parent layer not found: {normalizedParent}";
                        prepared.Add(preparedEntry);
                        continue;
                    }

                    if (parentSnapshot.Id == target.Id || IsDescendantPath(normalizedParent, target.FullPath))
                    {
                        preparedEntry.FailureMessage = $"Circular parent: {normalizedParent}";
                        prepared.Add(preparedEntry);
                        continue;
                    }

                    resolvedParentId = parentSnapshot.Id;
                    resolvedParentFullPath = parentSnapshot.FullPath;
                }
            }

            int? resolvedLinetypeIndex = null;
            if (!request.ClearLinetype && !string.IsNullOrWhiteSpace(request.LinetypeName))
            {
                resolvedLinetypeIndex = FindLiveLinetypeIndex(document, request.LinetypeName);
                if (resolvedLinetypeIndex is null)
                {
                    preparedEntry.FailureMessage = $"Linetype not found: {request.LinetypeName}";
                    prepared.Add(preparedEntry);
                    continue;
                }
            }

            int? resolvedMaterialIndex = null;
            if (!request.ClearRenderMaterial && !string.IsNullOrWhiteSpace(request.RenderMaterialName))
            {
                resolvedMaterialIndex = FindLiveMaterialIndex(document, request.RenderMaterialName);
                if (resolvedMaterialIndex is null)
                {
                    preparedEntry.FailureMessage = $"Render material not found: {request.RenderMaterialName}";
                    prepared.Add(preparedEntry);
                    continue;
                }
            }

            string resolvedNewFullPath = ComposeLayerPath(resolvedParentFullPath, resolvedName);
            preparedEntry.ResolvedNewFullPath = resolvedNewFullPath;
            preparedEntry.ResolvedName = resolvedName;
            preparedEntry.ResolvedParentLayerId = resolvedParentId;
            preparedEntry.ResolvedParentFullPath = resolvedParentFullPath;
            preparedEntry.ResolvedLinetypeIndex = resolvedLinetypeIndex;
            preparedEntry.ResolvedMaterialIndex = resolvedMaterialIndex;
            preparedEntry.FieldDiffs = BuildModificationDiffs(target, request, resolvedName, resolvedParentFullPath);
            prepared.Add(preparedEntry);
        }

        List<PreparedModifyEntry> validEntries = prepared.Where(item => item.CanApply).ToList();

        foreach (PreparedModifyEntry entry in validEntries)
        {
            if (snapshotByFullPath.TryGetValue(entry.ResolvedNewFullPath!, out LayerSnapshot? conflict)
                && conflict.Id != entry.TargetLayerId)
            {
                return OperationResponse<List<PreparedModifyEntry>>.Fail(
                    $"Modify batch conflict: target path already exists [{entry.ResolvedNewFullPath}]");
            }
        }

        var resolvedTargets = new Dictionary<string, PreparedModifyEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (PreparedModifyEntry entry in validEntries)
        {
            if (!resolvedTargets.TryAdd(entry.ResolvedNewFullPath!, entry))
            {
                return OperationResponse<List<PreparedModifyEntry>>.Fail(
                    $"Modify batch conflict: duplicate resolved target path [{entry.ResolvedNewFullPath}]");
            }
        }

        List<PreparedModifyEntry> structuralEntries = validEntries
            .Where(item => !string.Equals(item.RequestedFullPath, item.ResolvedNewFullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (int i = 0; i < structuralEntries.Count; i++)
        {
            for (int j = i + 1; j < structuralEntries.Count; j++)
            {
                if (IsDescendantPath(structuralEntries[i].RequestedFullPath, structuralEntries[j].RequestedFullPath)
                    || IsDescendantPath(structuralEntries[j].RequestedFullPath, structuralEntries[i].RequestedFullPath))
                {
                    return OperationResponse<List<PreparedModifyEntry>>.Fail(
                        $"Modify batch conflict: cannot rename or reparent both ancestor and descendant in one request [{structuralEntries[i].RequestedFullPath}] / [{structuralEntries[j].RequestedFullPath}]");
                }
            }
        }

        Dictionary<Guid, Guid> proposedParents = snapshotById.ToDictionary(item => item.Key, item => item.Value.ParentLayerId);
        foreach (PreparedModifyEntry entry in validEntries.Where(item => item.Request.NewParentFullPath is not null))
        {
            proposedParents[entry.TargetLayerId] = entry.ResolvedParentLayerId;
        }

        foreach (PreparedModifyEntry entry in validEntries)
        {
            var visited = new HashSet<Guid>();
            Guid current = entry.TargetLayerId;

            while (current != Guid.Empty && proposedParents.TryGetValue(current, out Guid next))
            {
                if (!visited.Add(current))
                {
                    return OperationResponse<List<PreparedModifyEntry>>.Fail(
                        $"Modify batch conflict: circular parent dependency detected for [{entry.RequestedFullPath}]");
                }

                current = next;
            }
        }

        return OperationResponse<List<PreparedModifyEntry>>.Ok(prepared);
    }

    private OperationResponse<List<PreparedDeleteTarget>> PrepareDeleteTargets(RhinoDoc document, IReadOnlyList<string> fullPaths)
    {
        if (fullPaths.Count == 0)
        {
            return OperationResponse<List<PreparedDeleteTarget>>.Fail("At least one layer full path is required.");
        }

        List<LayerSnapshot> snapshots = BuildLiveLayerSnapshots(document);
        Dictionary<string, LayerSnapshot> snapshotByFullPath = snapshots.ToDictionary(item => item.FullPath, StringComparer.OrdinalIgnoreCase);

        var prepared = new List<PreparedDeleteTarget>(fullPaths.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < fullPaths.Count; i++)
        {
            string requested = fullPaths[i]?.Trim() ?? string.Empty;
            var target = new PreparedDeleteTarget
            {
                OriginalOrder = i,
                RequestedFullPath = requested
            };

            string normalized;
            try
            {
                normalized = NormalizeLayerPath(requested);
                target.RequestedFullPath = normalized;
            }
            catch (Exception ex)
            {
                target.FailureMessage = ex.Message;
                prepared.Add(target);
                continue;
            }

            if (!seen.Add(normalized))
            {
                return OperationResponse<List<PreparedDeleteTarget>>.Fail($"Duplicate layer target: {normalized}");
            }

            if (!snapshotByFullPath.TryGetValue(normalized, out LayerSnapshot? layer))
            {
                target.FailureMessage = $"Layer not found: {normalized}";
                prepared.Add(target);
                continue;
            }

            List<LayerSnapshot> subtree = snapshots
                .Where(candidate => string.Equals(candidate.FullPath, normalized, StringComparison.OrdinalIgnoreCase)
                    || IsDescendantPath(candidate.FullPath, normalized))
                .OrderBy(candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            target.TargetLayerId = layer.Id;
            target.ParentLayerId = layer.ParentLayerId;
            target.ParentFullPath = layer.ParentFullPath;
            target.Depth = layer.Depth;
            target.DirectObjectCount = layer.ObjectCount;
            target.DescendantObjectCount = subtree.Where(item => item.Id != layer.Id).Sum(item => item.ObjectCount);
            target.TotalAffectedObjectCount = subtree.Sum(item => item.ObjectCount);
            target.CurrentLayerInSubtree = subtree.Any(item => item.IsCurrentLayer);
            target.SubtreeLayerIds = subtree.Select(item => item.Id).ToList();
            target.AffectedSubLayers = subtree
                .Where(item => item.Id != layer.Id)
                .Select(item => new LayerSubtreeEntry
                {
                    FullPath = item.FullPath,
                    DirectObjectCount = item.ObjectCount
                })
                .ToList();
            prepared.Add(target);
        }

        return OperationResponse<List<PreparedDeleteTarget>>.Ok(prepared);
    }

    private static LayerMutationResponse CreateMutationResponse(
        string filePath,
        int requestedCount,
        IReadOnlyList<LayerMutationResultResponse> results)
    {
        int succeeded = results.Count(result => result.Success);
        int changed = results.Count(result => result.Success && result.Changed);
        return new LayerMutationResponse
        {
            FilePath = filePath,
            RequestedCount = requestedCount,
            SucceededCount = succeeded,
            ChangedCount = changed,
            NoopCount = succeeded - changed,
            FailedCount = results.Count(result => !result.Success),
            Warnings = Array.Empty<ObjectEditWarning>(),
            Results = results
        };
    }

    private static LayerMutationResultResponse SuccessResult(string requestedFullPath, string? resolvedFullPath, string message, bool changed = true)
    {
        return new LayerMutationResultResponse
        {
            RequestedFullPath = requestedFullPath,
            ResolvedFullPath = resolvedFullPath,
            Success = true,
            Changed = changed,
            Message = message
        };
    }

    private static LayerMutationResultResponse FailResult(string requestedFullPath, string message)
    {
        return new LayerMutationResultResponse
        {
            RequestedFullPath = requestedFullPath,
            ResolvedFullPath = null,
            Success = false,
            Changed = false,
            Message = message
        };
    }

    private static LayerModificationImpactResponse BuildModificationImpact(
        PreparedModifyEntry entry,
        IReadOnlyDictionary<Guid, LayerSnapshot> snapshotLookup)
    {
        if (!entry.CanApply || entry.TargetSnapshot is null)
        {
            return new LayerModificationImpactResponse
            {
                TargetFullPath = entry.RequestedFullPath,
                ResolvedNewFullPath = entry.ResolvedNewFullPath,
                Success = false,
                Message = entry.FailureMessage ?? "Layer modification entry is invalid.",
                FieldDiffs = Array.Empty<string>(),
                AffectedSubLayers = Array.Empty<LayerSubtreeEntry>()
            };
        }

        IReadOnlyList<LayerSubtreeEntry> affectedSubLayers = snapshotLookup.Values
            .Where(item => item.Id != entry.TargetSnapshot.Id && IsDescendantPath(item.FullPath, entry.TargetSnapshot.FullPath))
            .OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(item => new LayerSubtreeEntry
            {
                FullPath = item.FullPath,
                DirectObjectCount = item.ObjectCount
            })
            .ToList();

        return new LayerModificationImpactResponse
        {
            TargetFullPath = entry.RequestedFullPath,
            ResolvedNewFullPath = entry.ResolvedNewFullPath,
            Success = true,
            Message = entry.FieldDiffs.Count == 0 ? "No changes would be applied." : "Layer modification preview generated.",
            FieldDiffs = entry.FieldDiffs,
            AffectedSubLayers = affectedSubLayers
        };
    }

    private static LayerDeletionImpactResponse BuildDeletionImpact(PreparedDeleteTarget target, bool isPurgePreview)
    {
        if (!target.CanApply)
        {
            return new LayerDeletionImpactResponse
            {
                TargetFullPath = target.RequestedFullPath,
                Success = false,
                Message = target.FailureMessage ?? "Layer preview entry is invalid."
            };
        }

        string message = isPurgePreview
            ? $"Purging [{target.RequestedFullPath}] would remove {target.TotalAffectedObjectCount} objects."
            : $"Deleting [{target.RequestedFullPath}] would move {target.TotalAffectedObjectCount} objects to the parent layer.";

        if (!isPurgePreview && target.ParentLayerId == Guid.Empty)
        {
            message += " Apply will reject this because the target is a root layer.";
        }

        if (target.CurrentLayerInSubtree)
        {
            message += " Apply will reject this because the current layer is inside the subtree.";
        }

        return new LayerDeletionImpactResponse
        {
            TargetFullPath = target.RequestedFullPath,
            Success = true,
            Message = message,
            DirectObjectCount = target.DirectObjectCount,
            DescendantObjectCount = target.DescendantObjectCount,
            TotalAffectedObjectCount = target.TotalAffectedObjectCount,
            CurrentLayerInSubtree = target.CurrentLayerInSubtree,
            AffectedSubLayers = target.AffectedSubLayers
        };
    }

    private static List<string> BuildModificationDiffs(
        LayerSnapshot snapshot,
        LayerModificationEntryRequest request,
        string resolvedName,
        string? resolvedParentFullPath)
    {
        var diffs = new List<string>();

        if (request.NewName is not null && !string.Equals(snapshot.LayerName, resolvedName, StringComparison.Ordinal))
        {
            diffs.Add($"Name: [{snapshot.LayerName}] -> [{resolvedName}]");
        }

        if (request.NewParentFullPath is not null && !string.Equals(snapshot.ParentFullPath, resolvedParentFullPath, StringComparison.OrdinalIgnoreCase))
        {
            diffs.Add($"ParentFullPath: [{snapshot.ParentFullPath ?? "<root>"}] -> [{resolvedParentFullPath ?? "<root>"}]");
        }

        if (request.Color is not null && !ColorsEqual(snapshot.Color, request.Color))
        {
            diffs.Add($"Color: {FormatColor(snapshot.Color)} -> {FormatColor(request.Color)}");
        }

        if (request.Visible.HasValue && snapshot.Visible != request.Visible.Value)
        {
            diffs.Add($"Visible: {snapshot.Visible} -> {request.Visible.Value}");
        }

        if (request.Locked.HasValue && snapshot.Locked != request.Locked.Value)
        {
            diffs.Add($"Locked: {snapshot.Locked} -> {request.Locked.Value}");
        }

        if (request.ClearPlotColor)
        {
            if (snapshot.PlotColor is not null)
            {
                diffs.Add($"PlotColor: {FormatColor(snapshot.PlotColor)} -> <inherit/display color>");
            }
        }
        else if (request.PlotColor is not null && !ColorsEqual(snapshot.PlotColor, request.PlotColor))
        {
            diffs.Add($"PlotColor: {FormatColor(snapshot.PlotColor)} -> {FormatColor(request.PlotColor)}");
        }

        if (request.ClearPlotWeight)
        {
            if (snapshot.PlotWeight.HasValue)
            {
                diffs.Add($"PlotWeight: {FormatWeight(snapshot.PlotWeight)} -> <default>");
            }
        }
        else if (request.PlotWeight.HasValue && snapshot.PlotWeight != request.PlotWeight.Value)
        {
            diffs.Add($"PlotWeight: {FormatWeight(snapshot.PlotWeight)} -> {request.PlotWeight.Value}");
        }

        if (request.ClearLinetype)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.LinetypeName))
            {
                diffs.Add($"LinetypeName: [{snapshot.LinetypeName}] -> <default>");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.LinetypeName) && !string.Equals(snapshot.LinetypeName, request.LinetypeName, StringComparison.Ordinal))
        {
            diffs.Add($"LinetypeName: [{snapshot.LinetypeName ?? "<default>"}] -> [{request.LinetypeName}]");
        }

        if (request.ClearRenderMaterial)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.RenderMaterialName))
            {
                diffs.Add($"RenderMaterialName: [{snapshot.RenderMaterialName}] -> <default>");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RenderMaterialName)
            && !string.Equals(snapshot.RenderMaterialName, request.RenderMaterialName, StringComparison.Ordinal))
        {
            diffs.Add($"RenderMaterialName: [{snapshot.RenderMaterialName ?? "<default>"}] -> [{request.RenderMaterialName}]");
        }

        return diffs;
    }

    private static void ApplyPreparedModification(LiveLayer layer, PreparedModifyEntry entry)
    {
        LayerModificationEntryRequest request = entry.Request;

        if (request.NewName is not null)
        {
            layer.Name = entry.ResolvedName!;
        }

        if (request.NewParentFullPath is not null)
        {
            layer.ParentLayerId = entry.ResolvedParentLayerId;
        }

        if (request.Color is not null)
        {
            layer.Color = request.Color.ToColor();
        }

        if (request.Visible.HasValue)
        {
            layer.IsVisible = request.Visible.Value;
        }

        if (request.Locked.HasValue)
        {
            layer.IsLocked = request.Locked.Value;
        }

        if (request.ClearPlotColor)
        {
            layer.DeletePlotColor();
        }
        else if (request.PlotColor is not null)
        {
            layer.PlotColor = request.PlotColor.ToColor();
        }

        if (request.ClearPlotWeight)
        {
            layer.PlotWeight = 0d;
        }
        else if (request.PlotWeight.HasValue)
        {
            layer.PlotWeight = request.PlotWeight.Value;
        }

        if (request.ClearLinetype)
        {
            layer.LinetypeIndex = -1;
        }
        else if (entry.ResolvedLinetypeIndex.HasValue)
        {
            layer.LinetypeIndex = entry.ResolvedLinetypeIndex.Value;
        }

        if (request.ClearRenderMaterial)
        {
            layer.RenderMaterialIndex = -1;
        }
        else if (entry.ResolvedMaterialIndex.HasValue)
        {
            layer.RenderMaterialIndex = entry.ResolvedMaterialIndex.Value;
        }
    }

    private static void ApplyCommonFields(
        LiveLayer layer,
        LayerCreationEntryRequest request,
        int? resolvedLinetypeIndex,
        int? resolvedMaterialIndex)
    {
        if (request.Color is not null)
        {
            layer.Color = request.Color.ToColor();
        }

        if (request.Visible.HasValue)
        {
            layer.IsVisible = request.Visible.Value;
        }

        if (request.Locked.HasValue)
        {
            layer.IsLocked = request.Locked.Value;
        }

        if (request.PlotColor is not null)
        {
            layer.PlotColor = request.PlotColor.ToColor();
        }

        if (request.PlotWeight.HasValue)
        {
            layer.PlotWeight = request.PlotWeight.Value;
        }

        if (resolvedLinetypeIndex.HasValue)
        {
            layer.LinetypeIndex = resolvedLinetypeIndex.Value;
        }

        if (resolvedMaterialIndex.HasValue)
        {
            layer.RenderMaterialIndex = resolvedMaterialIndex.Value;
        }
    }

    private static HashSet<string> GetLiveLayerPaths(RhinoDoc document)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                paths.Add(layer.FullPath);
            }
        }

        return paths;
    }

    private static List<Guid> CollectCurrentObjectIds(RhinoDoc document, IReadOnlySet<Guid> subtreeLayerIds)
    {
        var layerIndexLookup = new HashSet<int>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted && subtreeLayerIds.Contains(layer.Id))
            {
                layerIndexLookup.Add(layer.Index);
            }
        }

        var objectIds = new List<Guid>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (!rhinoObject.IsDeleted && layerIndexLookup.Contains(rhinoObject.Attributes.LayerIndex))
            {
                objectIds.Add(rhinoObject.Id);
            }
        }

        return objectIds;
    }

    private static CurrentSubtreeImpact BuildCurrentSubtreeImpact(RhinoDoc document, Guid targetLayerId)
    {
        Dictionary<Guid, LiveLayer> layersById = BuildLiveLayerLookup(document);
        if (!layersById.TryGetValue(targetLayerId, out LiveLayer? targetLayer))
        {
            return new CurrentSubtreeImpact();
        }

        List<Guid> subtreeIds = GetCurrentSubtreeLayerIds(layersById, targetLayerId);
        HashSet<int> subtreeIndices = subtreeIds
            .Select(id => layersById[id].Index)
            .ToHashSet();

        int totalObjects = 0;
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (!rhinoObject.IsDeleted && subtreeIndices.Contains(rhinoObject.Attributes.LayerIndex))
            {
                totalObjects++;
            }
        }

        return new CurrentSubtreeImpact
        {
            TotalObjectCount = totalObjects,
            DescendantLayerCount = Math.Max(0, subtreeIds.Count - 1)
        };
    }

    private static List<Guid> GetCurrentSubtreeLayerIds(IReadOnlyDictionary<Guid, LiveLayer> layersById, Guid targetLayerId)
    {
        if (!layersById.TryGetValue(targetLayerId, out LiveLayer? targetLayer))
        {
            return new List<Guid>();
        }

        return layersById.Values
            .Where(layer => string.Equals(layer.FullPath, targetLayer.FullPath, StringComparison.OrdinalIgnoreCase)
                || IsDescendantPath(layer.FullPath, targetLayer.FullPath))
            .Select(layer => layer.Id)
            .ToList();
    }

    private static Dictionary<Guid, LiveLayer> BuildLiveLayerLookup(RhinoDoc document)
    {
        var layersById = new Dictionary<Guid, LiveLayer>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                layersById[layer.Id] = layer;
            }
        }

        return layersById;
    }

    private static List<LayerSnapshot> BuildLiveLayerSnapshots(RhinoDoc document)
    {
        Dictionary<int, int> objectCounts = BuildLiveObjectCounts(document);
        Dictionary<int, string> linetypeNames = BuildLiveLinetypeNames(document);
        Dictionary<int, string> materialNames = BuildLiveMaterialNames(document);
        Dictionary<Guid, string> layerPathsById = new();

        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                layerPathsById[layer.Id] = layer.FullPath;
            }
        }

        var snapshots = new List<LayerSnapshot>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            LiveLayer layer = document.Layers[i];
            if (layer.IsDeleted)
            {
                continue;
            }

            snapshots.Add(new LayerSnapshot
            {
                Id = layer.Id,
                Index = layer.Index,
                LayerName = layer.Name,
                FullPath = layer.FullPath,
                ParentLayerId = layer.ParentLayerId,
                ParentFullPath = layer.ParentLayerId == Guid.Empty || !layerPathsById.TryGetValue(layer.ParentLayerId, out string? parentPath)
                    ? null
                    : parentPath,
                Depth = GetLayerDepth(layer.FullPath),
                ObjectCount = objectCounts.GetValueOrDefault(layer.Index),
                Color = ToDisplayColor(layer.Color),
                Visible = layer.IsVisible,
                Locked = layer.IsLocked,
                IsCurrentLayer = layer.Index == document.Layers.CurrentLayerIndex,
                PlotColor = InferPlotColor(layer),
                PlotWeight = InferPlotWeight(layer.PlotWeight),
                LinetypeName = layer.LinetypeIndex >= 0 ? linetypeNames.GetValueOrDefault(layer.LinetypeIndex) : null,
                RenderMaterialName = layer.RenderMaterialIndex >= 0 ? materialNames.GetValueOrDefault(layer.RenderMaterialIndex) : null
            });
        }

        return snapshots.OrderBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static RhinoLayerDetail BuildOfflineDetail(
        File3dmLayer layer,
        IReadOnlyDictionary<int, int> objectCounts,
        IReadOnlyDictionary<int, string> linetypeNames,
        IReadOnlyDictionary<int, string> materialNames,
        IReadOnlyDictionary<Guid, string> layerPathsById)
    {
        return new RhinoLayerDetail
        {
            LayerIndex = layer.Index,
            LayerName = layer.Name,
            FullPath = layer.FullPath,
            ParentFullPath = layer.ParentLayerId == Guid.Empty || !layerPathsById.TryGetValue(layer.ParentLayerId, out string? parentPath)
                ? null
                : parentPath,
            ObjectCount = objectCounts.GetValueOrDefault(layer.Index),
            Color = ToDisplayColor(layer.Color),
            Visible = layer.IsVisible,
            Locked = layer.IsLocked,
            IsCurrentLayer = false,
            PlotColor = InferPlotColor(layer.Color, layer.PlotColor),
            PlotWeight = InferPlotWeight(layer.PlotWeight),
            LinetypeName = layer.LinetypeIndex >= 0 ? linetypeNames.GetValueOrDefault(layer.LinetypeIndex) : null,
            RenderMaterialName = layer.RenderMaterialIndex >= 0 ? materialNames.GetValueOrDefault(layer.RenderMaterialIndex) : null
        };
    }

    private static Dictionary<int, int> BuildOfflineObjectCounts(File3dm model)
    {
        return model.Objects
            .GroupBy(item => item.Attributes.LayerIndex)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static Dictionary<int, int> BuildLiveObjectCounts(RhinoDoc document)
    {
        var counts = new Dictionary<int, int>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (rhinoObject.IsDeleted)
            {
                continue;
            }

            int layerIndex = rhinoObject.Attributes.LayerIndex;
            counts[layerIndex] = counts.GetValueOrDefault(layerIndex) + 1;
        }

        return counts;
    }

    private static Dictionary<int, string> BuildOfflineLinetypeNames(File3dm model)
    {
        var result = new Dictionary<int, string>();
        foreach (File3dmLinetype linetype in model.AllLinetypes)
        {
            if (!linetype.IsDeleted && !string.IsNullOrWhiteSpace(linetype.Name))
            {
                result[linetype.Index] = linetype.Name;
            }
        }

        return result;
    }

    private static Dictionary<int, string> BuildLiveLinetypeNames(RhinoDoc document)
    {
        var result = new Dictionary<int, string>();
        for (int i = 0; i < document.Linetypes.Count; i++)
        {
            var linetype = document.Linetypes[i];
            if (!linetype.IsDeleted && !string.IsNullOrWhiteSpace(linetype.Name))
            {
                result[linetype.LinetypeIndex] = linetype.Name;
            }
        }

        return result;
    }

    private static Dictionary<int, string> BuildOfflineMaterialNames(File3dm model)
    {
        var result = new Dictionary<int, string>();
        foreach (File3dmMaterial material in model.AllMaterials)
        {
            if (!material.IsDeleted && !string.IsNullOrWhiteSpace(material.Name))
            {
                result[material.Index] = material.Name;
            }
        }

        return result;
    }

    private static Dictionary<int, string> BuildLiveMaterialNames(RhinoDoc document)
    {
        var result = new Dictionary<int, string>();
        for (int i = 0; i < document.Materials.Count; i++)
        {
            var material = document.Materials[i];
            if (!material.IsDeleted && !string.IsNullOrWhiteSpace(material.Name))
            {
                result[material.Index] = material.Name;
            }
        }

        return result;
    }

    private static int? FindLiveLinetypeIndex(RhinoDoc document, string name)
    {
        for (int i = 0; i < document.Linetypes.Count; i++)
        {
            var linetype = document.Linetypes[i];
            if (!linetype.IsDeleted && string.Equals(linetype.Name, name, StringComparison.Ordinal))
            {
                return linetype.LinetypeIndex;
            }
        }

        return null;
    }

    private static int? FindLiveMaterialIndex(RhinoDoc document, string name)
    {
        for (int i = 0; i < document.Materials.Count; i++)
        {
            var material = document.Materials[i];
            if (!material.IsDeleted && string.Equals(material.Name, name, StringComparison.Ordinal))
            {
                return material.Index;
            }
        }

        return null;
    }

    private static int FindLiveLayerIndex(RhinoDoc document, Guid layerId)
    {
        return document.Layers.Find(layerId, true, -1);
    }

    private static int SafeGetLayerDepth(RhinoDoc document, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= document.Layers.Count)
        {
            return -1;
        }

        return GetLayerDepth(document.Layers[layerIndex].FullPath);
    }

    private List<ObjectEditWarning> CreateOfflineWarnings(string filePath)
    {
        var warnings = new List<ObjectEditWarning>();
        if (_documentAccessor.TryGetActiveDocumentState(filePath, out bool hasUnsavedChanges) && hasUnsavedChanges)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "OFFLINE_READ_STALE",
                Message = "The target file is open in Rhino with unsaved changes, so offline read results may be stale."
            });
        }

        return warnings;
    }

    private static string NormalizeLayerPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Layer full path cannot be empty.");
        }

        string trimmed = value.Trim();
        string[] segments = trimmed.Split(new[] { "::" }, StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(segment => string.IsNullOrWhiteSpace(segment)))
        {
            throw new InvalidOperationException($"Invalid layer full path: {value}");
        }

        List<string> normalizedSegments = new(segments.Length);
        foreach (string segment in segments)
        {
            string normalized = segment.Trim();
            if (normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid layer name segment: {segment}");
            }

            normalizedSegments.Add(normalized);
        }

        return string.Join("::", normalizedSegments);
    }

    private static string NormalizeLayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("New layer name cannot be empty.");
        }

        string normalized = value.Trim();
        if (normalized.Contains("::", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid layer name: {value}");
        }

        return normalized;
    }

    private static int GetLayerDepth(string fullPath)
    {
        return fullPath.Split(new[] { "::" }, StringSplitOptions.None).Length;
    }

    private static string ComposeLayerPath(string? parentFullPath, string layerName)
    {
        return string.IsNullOrWhiteSpace(parentFullPath) ? layerName : $"{parentFullPath}::{layerName}";
    }

    private static bool IsDescendantPath(string candidatePath, string ancestorPath)
    {
        return candidatePath.Length > ancestorPath.Length
            && candidatePath.StartsWith(ancestorPath + "::", StringComparison.OrdinalIgnoreCase);
    }

    private static RhinoDisplayColor ToDisplayColor(Color color)
    {
        return new RhinoDisplayColor
        {
            R = color.R,
            G = color.G,
            B = color.B
        };
    }

    private static RhinoDisplayColor? InferPlotColor(LiveLayer layer)
    {
        return InferPlotColor(layer.Color, layer.PlotColor);
    }

    private static RhinoDisplayColor? InferPlotColor(Color displayColor, Color plotColor)
    {
        return displayColor.ToArgb() == plotColor.ToArgb()
            ? null
            : ToDisplayColor(plotColor);
    }

    private static double? InferPlotWeight(double plotWeight)
    {
        return Math.Abs(plotWeight) < double.Epsilon ? null : plotWeight;
    }

    private static bool ColorsEqual(RhinoDisplayColor? left, RhinoDisplayColor? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.R == right.R && left.G == right.G && left.B == right.B;
    }

    private static string FormatColor(RhinoDisplayColor? color)
    {
        return color is null ? "<default>" : $"({color.R},{color.G},{color.B})";
    }

    private static string FormatWeight(double? weight)
    {
        return weight?.ToString() ?? "<default>";
    }

    private sealed class PreparedCreateEntry
    {
        public LayerCreationEntryRequest Request { get; init; } = new();
        public string FullPath { get; init; } = string.Empty;
        public int Depth { get; init; }
        public int? ResolvedLinetypeIndex { get; set; }
        public int? ResolvedMaterialIndex { get; set; }
    }

    private sealed class LayerSnapshot
    {
        public Guid Id { get; init; }
        public int Index { get; init; }
        public string LayerName { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public Guid ParentLayerId { get; init; }
        public string? ParentFullPath { get; init; }
        public int Depth { get; init; }
        public int ObjectCount { get; init; }
        public RhinoDisplayColor Color { get; init; } = new();
        public bool Visible { get; init; }
        public bool Locked { get; init; }
        public bool IsCurrentLayer { get; init; }
        public RhinoDisplayColor? PlotColor { get; init; }
        public double? PlotWeight { get; init; }
        public string? LinetypeName { get; init; }
        public string? RenderMaterialName { get; init; }
    }

    private sealed class PreparedModifyEntry
    {
        public int OriginalOrder { get; init; }
        public LayerModificationEntryRequest Request { get; init; } = new();
        public string RequestedFullPath { get; set; } = string.Empty;
        public Guid TargetLayerId { get; set; }
        public LayerSnapshot? TargetSnapshot { get; set; }
        public string? ResolvedName { get; set; }
        public Guid ResolvedParentLayerId { get; set; }
        public string? ResolvedParentFullPath { get; set; }
        public string? ResolvedNewFullPath { get; set; }
        public int? ResolvedLinetypeIndex { get; set; }
        public int? ResolvedMaterialIndex { get; set; }
        public string? FailureMessage { get; set; }
        public IReadOnlyList<string> FieldDiffs { get; set; } = Array.Empty<string>();
        public bool CanApply => TargetSnapshot is not null && string.IsNullOrWhiteSpace(FailureMessage);
    }

    private sealed class PreparedDeleteTarget
    {
        public int OriginalOrder { get; init; }
        public string RequestedFullPath { get; set; } = string.Empty;
        public Guid TargetLayerId { get; set; }
        public Guid ParentLayerId { get; set; }
        public string? ParentFullPath { get; set; }
        public int Depth { get; set; }
        public int DirectObjectCount { get; set; }
        public int DescendantObjectCount { get; set; }
        public int TotalAffectedObjectCount { get; set; }
        public bool CurrentLayerInSubtree { get; set; }
        public List<Guid> SubtreeLayerIds { get; set; } = new();
        public IReadOnlyList<LayerSubtreeEntry> AffectedSubLayers { get; set; } = Array.Empty<LayerSubtreeEntry>();
        public string? FailureMessage { get; set; }
        public bool CanApply => string.IsNullOrWhiteSpace(FailureMessage);
    }

    private sealed class CurrentSubtreeImpact
    {
        public int TotalObjectCount { get; init; }
        public int DescendantLayerCount { get; init; }
    }
}
