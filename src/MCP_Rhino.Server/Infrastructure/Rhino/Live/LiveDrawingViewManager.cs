extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;
using ViewInfo = rhinocommon::Rhino.DocObjects.ViewInfo;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveDrawingViewManager : ILiveDrawingViewManager
{
    public OperationResponse<DrawingLayerScopeResult> ResolveLayerScope(
        RhinoDoc document,
        IReadOnlyList<string> layerQueries,
        IReadOnlyList<string> confirmedLayerFullPaths)
    {
        Dictionary<int, int> objectCounts = BuildVisibleLayerObjectCounts(document);
        Dictionary<string, rhinocommon::Rhino.DocObjects.Layer> layersByFullPath = BuildLayerLookup(document);
        var resolvedLayers = new Dictionary<int, rhinocommon::Rhino.DocObjects.Layer>();
        var candidateLayers = new List<RhinoLayerCandidate>();

        foreach (string fullPath in confirmedLayerFullPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (layersByFullPath.TryGetValue(fullPath.Trim(), out rhinocommon::Rhino.DocObjects.Layer? layer))
            {
                resolvedLayers[layer.Index] = layer;
            }
            else
            {
                candidateLayers.AddRange(FindLayerCandidates(document, fullPath, objectCounts, exactMatch: false));
            }
        }

        foreach (string query in layerQueries.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            List<RhinoLayerCandidate> matches = FindLayerCandidates(document, query, objectCounts, exactMatch: false);
            if (matches.Count == 1)
            {
                if (layersByFullPath.TryGetValue(matches[0].FullPath, out rhinocommon::Rhino.DocObjects.Layer? layer))
                {
                    resolvedLayers[layer.Index] = layer;
                }
                continue;
            }

            candidateLayers.AddRange(matches);
        }

        bool noLayerInput = confirmedLayerFullPaths.All(string.IsNullOrWhiteSpace)
            && layerQueries.All(string.IsNullOrWhiteSpace);

        if (noLayerInput)
        {
            candidateLayers.AddRange(ListAllCandidateLayers(document, objectCounts));
        }

        if (candidateLayers.Count > 0 || (noLayerInput && resolvedLayers.Count == 0))
        {
            return OperationResponse<DrawingLayerScopeResult>.Ok(new DrawingLayerScopeResult
            {
                NeedsLayerSelection = true,
                CandidateLayers = candidateLayers
                    .GroupBy(candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            }, "Drawing export requires layer selection.");
        }

        return OperationResponse<DrawingLayerScopeResult>.Ok(new DrawingLayerScopeResult
        {
            ResolvedLayerFullPaths = resolvedLayers.Values
                .Select(layer => layer.FullPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ResolvedLayerIndices = resolvedLayers.Keys.ToList()
        });
    }

    public OperationResponse<DrawingViewSetupResult> SetupViews(
        RhinoDoc document,
        DrawingViewPreset preset,
        IReadOnlyList<int> layerIndices,
        IReadOnlyList<string> resolvedLayerFullPaths,
        double fitMarginPercent,
        IReadOnlyList<Guid>? requestedObjectIds = null)
    {
        if (preset is not DrawingViewPreset.Standard8 and not DrawingViewPreset.Isometric4)
        {
            return OperationResponse<DrawingViewSetupResult>.Fail($"Unsupported drawing view preset: {preset}");
        }

        RhinoView? activeView = document.Views.ActiveView;
        if (activeView is null)
        {
            return OperationResponse<DrawingViewSetupResult>.Fail("EXPORT_VIEW_NOT_FOUND");
        }

        OperationResponse<(BoundingBox BoundingBox, IReadOnlyList<Guid> ObjectIds)> target = ResolveTargetBoundingBox(
            document,
            layerIndices,
            fitMarginPercent,
            requestedObjectIds);
        if (!target.Success)
        {
            return OperationResponse<DrawingViewSetupResult>.Fail(target.Message);
        }

        (BoundingBox targetBox, IReadOnlyList<Guid> targetObjectIds) = target.Data;
        var created = new List<string>();
        var updated = new List<string>();
        List<DrawingViewDefinition> definitions = CreateViewDefinitions(preset);

        using var originalView = new ViewInfo(activeView.MainViewport);
        try
        {
            foreach (DrawingViewDefinition definition in definitions)
            {
                int existingIndex = document.NamedViews.FindByName(definition.Name);
                ApplyView(activeView, definition, targetBox);

                using var viewInfo = new ViewInfo(activeView.MainViewport)
                {
                    Name = definition.Name
                };

                if (existingIndex >= 0)
                {
                    document.NamedViews.Delete(existingIndex);
                    updated.Add(definition.Name);
                }
                else
                {
                    created.Add(definition.Name);
                }

                int addedIndex = document.NamedViews.Add(viewInfo);
                if (addedIndex < 0)
                {
                    return OperationResponse<DrawingViewSetupResult>.Fail($"DRAWING_VIEW_CREATE_FAILED: {definition.Name}");
                }
            }
        }
        finally
        {
            activeView.MainViewport.PushViewInfo(originalView, false);
            document.Views.Redraw();
        }

        return OperationResponse<DrawingViewSetupResult>.Ok(new DrawingViewSetupResult
        {
            ResolvedLayerFullPaths = resolvedLayerFullPaths,
            CreatedViewNames = created,
            UpdatedViewNames = updated,
            ViewNames = definitions.Select(definition => definition.Name).ToList(),
            TargetObjectCount = targetObjectIds.Count,
            TargetObjectIds = targetObjectIds,
            Warnings = Array.Empty<ObjectEditWarning>()
        }, "Drawing views set up.");
    }

    private static Dictionary<string, rhinocommon::Rhino.DocObjects.Layer> BuildLayerLookup(RhinoDoc document)
    {
        var layers = new Dictionary<string, rhinocommon::Rhino.DocObjects.Layer>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < document.Layers.Count; i++)
        {
            rhinocommon::Rhino.DocObjects.Layer layer = document.Layers[i];
            if (!layer.IsDeleted)
            {
                layers[layer.FullPath] = layer;
            }
        }

        return layers;
    }

    private static Dictionary<int, int> BuildVisibleLayerObjectCounts(RhinoDoc document)
    {
        var counts = new Dictionary<int, int>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (!IsVisibleObject(rhinoObject))
            {
                continue;
            }

            int layerIndex = rhinoObject.Attributes.LayerIndex;
            counts[layerIndex] = counts.TryGetValue(layerIndex, out int existing) ? existing + 1 : 1;
        }

        return counts;
    }

    private static List<RhinoLayerCandidate> ListAllCandidateLayers(RhinoDoc document, IReadOnlyDictionary<int, int> objectCounts)
    {
        var candidates = new List<RhinoLayerCandidate>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            rhinocommon::Rhino.DocObjects.Layer layer = document.Layers[i];
            if (layer.IsDeleted)
            {
                continue;
            }

            objectCounts.TryGetValue(layer.Index, out int objectCount);
            candidates.Add(ToCandidate(layer, objectCount));
        }

        return candidates;
    }

    private static List<RhinoLayerCandidate> FindLayerCandidates(
        RhinoDoc document,
        string query,
        IReadOnlyDictionary<int, int> objectCounts,
        bool exactMatch)
    {
        var candidates = new List<RhinoLayerCandidate>();
        for (int i = 0; i < document.Layers.Count; i++)
        {
            rhinocommon::Rhino.DocObjects.Layer layer = document.Layers[i];
            if (layer.IsDeleted || !MatchesLayer(layer, query, exactMatch))
            {
                continue;
            }

            objectCounts.TryGetValue(layer.Index, out int objectCount);
            candidates.Add(ToCandidate(layer, objectCount));
        }

        return candidates
            .OrderBy(candidate => candidate.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RhinoLayerCandidate ToCandidate(rhinocommon::Rhino.DocObjects.Layer layer, int objectCount)
    {
        return new RhinoLayerCandidate
        {
            LayerIndex = layer.Index,
            LayerName = layer.Name,
            FullPath = layer.FullPath,
            ObjectCount = objectCount
        };
    }

    private static bool MatchesLayer(rhinocommon::Rhino.DocObjects.Layer layer, string query, bool exactMatch)
    {
        if (exactMatch)
        {
            return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(layer.FullPath, query, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(layer.Name, query, StringComparison.OrdinalIgnoreCase)
            || layer.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static OperationResponse<(BoundingBox BoundingBox, IReadOnlyList<Guid> ObjectIds)> ResolveTargetBoundingBox(
        RhinoDoc document,
        IReadOnlyList<int> layerIndices,
        double fitMarginPercent,
        IReadOnlyList<Guid>? targetObjectIds)
    {
        HashSet<int> layerIndexSet = layerIndices.ToHashSet();
        HashSet<Guid>? objectIdSet = targetObjectIds is { Count: > 0 } ? targetObjectIds.ToHashSet() : null;
        BoundingBox? union = null;
        var objectIds = new List<Guid>();

        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (!IsVisibleObject(rhinoObject)
                || !layerIndexSet.Contains(rhinoObject.Attributes.LayerIndex)
                || (objectIdSet is not null && !objectIdSet.Contains(rhinoObject.Id)))
            {
                continue;
            }

            BoundingBox objectBox = rhinoObject.Geometry.GetBoundingBox(true);
            if (!objectBox.IsValid)
            {
                continue;
            }

            union = union.HasValue ? BoundingBox.Union(union.Value, objectBox) : objectBox;
            objectIds.Add(rhinoObject.Id);
        }

        if (!union.HasValue || objectIds.Count == 0)
        {
            return OperationResponse<(BoundingBox, IReadOnlyList<Guid>)>.Fail("DRAWING_VIEW_TARGET_EMPTY");
        }

        BoundingBox inflated = union.Value;
        double maxDimension = Math.Max(Math.Max(inflated.Diagonal.X, inflated.Diagonal.Y), inflated.Diagonal.Z);
        double margin = Math.Max(maxDimension * Math.Max(0d, fitMarginPercent) / 100d, 1e-6d);
        inflated.Inflate(margin);

        return OperationResponse<(BoundingBox, IReadOnlyList<Guid>)>.Ok((inflated, objectIds));
    }

    private static bool IsVisibleObject(RhinoObject rhinoObject)
    {
        return !rhinoObject.IsDeleted
            && !rhinoObject.IsHidden
            && rhinoObject.Attributes.Visible
            && rhinoObject.Geometry is not null;
    }

    private static void ApplyView(RhinoView view, DrawingViewDefinition definition, BoundingBox targetBox)
    {
        Vector3d direction = new(definition.DirectionX, definition.DirectionY, definition.DirectionZ);
        direction.Unitize();
        Point3d target = targetBox.Center;
        double distance = Math.Max(targetBox.Diagonal.Length * 2d, 1d);
        Point3d camera = target - (direction * distance);

        view.MainViewport.Name = definition.Name;
        view.MainViewport.SetCameraLocations(camera, target);
        view.MainViewport.SetCameraDirection(direction, true);
        view.MainViewport.ChangeToParallelProjection(true);
        view.MainViewport.ZoomBoundingBox(targetBox);
        view.Redraw();
    }

    private static List<DrawingViewDefinition> CreateViewDefinitions(DrawingViewPreset preset)
    {
        var isometric = new List<DrawingViewDefinition>
        {
            Create("MCP_Iso_NE", DrawingViewKind.Isometric, -1d, -1d, -1d, 0d, 0d, 1d),
            Create("MCP_Iso_NW", DrawingViewKind.Isometric, 1d, -1d, -1d, 0d, 0d, 1d),
            Create("MCP_Iso_SE", DrawingViewKind.Isometric, -1d, 1d, -1d, 0d, 0d, 1d),
            Create("MCP_Iso_SW", DrawingViewKind.Isometric, 1d, 1d, -1d, 0d, 0d, 1d)
        };

        if (preset == DrawingViewPreset.Isometric4)
        {
            return isometric;
        }

        return new List<DrawingViewDefinition>
        {
            Create("MCP_Elevation_Front", DrawingViewKind.Elevation, 0d, -1d, 0d, 0d, 0d, 1d),
            Create("MCP_Elevation_Back", DrawingViewKind.Elevation, 0d, 1d, 0d, 0d, 0d, 1d),
            Create("MCP_Elevation_Left", DrawingViewKind.Elevation, 1d, 0d, 0d, 0d, 0d, 1d),
            Create("MCP_Elevation_Right", DrawingViewKind.Elevation, -1d, 0d, 0d, 0d, 0d, 1d)
        }.Concat(isometric).ToList();
    }

    private static DrawingViewDefinition Create(
        string name,
        DrawingViewKind kind,
        double directionX,
        double directionY,
        double directionZ,
        double upX,
        double upY,
        double upZ)
    {
        return new DrawingViewDefinition
        {
            Name = name,
            Kind = kind,
            DirectionX = directionX,
            DirectionY = directionY,
            DirectionZ = directionZ,
            UpX = upX,
            UpY = upY,
            UpZ = upZ
        };
    }
}
