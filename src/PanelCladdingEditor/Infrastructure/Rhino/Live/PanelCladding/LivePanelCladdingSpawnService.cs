extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed partial class LivePanelCladdingSpawnService : ILivePanelCladdingSpawnService
{
    private readonly ILivePanelCladdingRepository _layoutRepository;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingSpawnPlanningService _planning;
    private readonly PanelCladdingExtrusionPlanningService _extrusionPlanning = new();

    public LivePanelCladdingSpawnService(
        ILivePanelCladdingRepository layoutRepository,
        PanelCladdingKeyService keys,
        PanelCladdingSpawnPlanningService planning)
    {
        _layoutRepository = layoutRepository;
        _keys = keys;
        _planning = planning;
    }

    public OperationResponse<PanelCladdingSpawnResult> Spawn(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        PanelCladdingObjectScope scope)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingSpawnResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        Guid[] sourcePanelIds = (objectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (sourcePanelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingSpawnResult>.Fail("PANEL_CLADDING_PANEL_SELECTION_REQUIRED");
        }

        var preparedRegions = new List<PreparedRegion>();
        var preparedCurves = new List<PreparedCurve>();
        try
        {
            foreach (Guid sourcePanelId in sourcePanelIds)
            {
                OperationResponse<PreparedPanel> panel = PreparePanel(
                    document,
                    filePath,
                    sourcePanelId,
                    scope);
                if (!panel.Success || panel.Data is null)
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_PANEL_PREPARE_FAILED: {sourcePanelId:D}: {panel.Message}");
                }
                preparedRegions.AddRange(panel.Data.Regions);
                preparedCurves.AddRange(panel.Data.Curves);
            }

            string? duplicateCid = preparedRegions.Select(item => item.Region.Cid)
                .Concat(preparedCurves.Select(item => item.CurvePlan.Cid))
                .GroupBy(cid => cid, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (duplicateCid is not null)
            {
                return OperationResponse<PanelCladdingSpawnResult>.Fail(
                    $"PANEL_CLADDING_DUPLICATE_BATCH_CID: {duplicateCid}");
            }
            foreach (string cid in preparedRegions.Select(item => item.Region.Cid)
                .Concat(preparedCurves.Select(item => item.CurvePlan.Cid)))
            {
                RhinoObject[]? existing = document.Objects.FindByUserString(
                    PanelCladdingSpawnPlanningService.CidUserTextKey,
                    cid,
                    caseSensitive: false);
                if (existing is { Length: > 0 } && existing.Any(item =>
                        IsManagedCidConflict(document, item, scope)))
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_CID_ALREADY_EXISTS: {cid}");
                }
            }

            uint undoRecord = document.BeginUndoRecord(
                scope == PanelCladdingObjectScope.Surfaces
                    ? "Spawn Panel Cladding Surfaces"
                    : "Spawn Panel Cladding Curves");
            var createdIds = new List<Guid>(preparedRegions.Count + preparedCurves.Count);
            var surfaceIds = new List<Guid>(preparedRegions.Count);
            var curveIds = new List<Guid>(preparedCurves.Count);
            try
            {
                foreach (PreparedRegion item in preparedRegions)
                {
                    OperationResponse<int> layer = EnsureMaterialLayer(document, item.Region);
                    if (!layer.Success)
                    {
                        RollBackCreatedObjects(document, createdIds);
                        return OperationResponse<PanelCladdingSpawnResult>.Fail(layer.Message);
                    }

                    var attributes = new ObjectAttributes
                    {
                        Name = item.Region.Cid,
                        LayerIndex = layer.Data,
                        ColorSource = ObjectColorSource.ColorFromLayer
                    };
                    foreach ((string key, string value) in item.Region.UserTextWrites)
                    {
                        attributes.SetUserString(key, value);
                    }

                    Guid createdId = document.Objects.AddBrep(item.Geometry, attributes);
                    if (createdId == Guid.Empty)
                    {
                        RollBackCreatedObjects(document, createdIds);
                        return OperationResponse<PanelCladdingSpawnResult>.Fail(
                            $"PANEL_CLADDING_OBJECT_CREATE_FAILED: {item.Region.Cid}");
                    }
                    createdIds.Add(createdId);
                    surfaceIds.Add(createdId);
                }

                if (preparedCurves.Count > 0)
                {
                    var curveLayers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (PreparedCurve item in preparedCurves)
                    {
                        if (!curveLayers.TryGetValue(item.CurvePlan.LayerPath, out int curveLayerIndex))
                        {
                            OperationResponse<int> curveLayer = EnsureLayer(
                                document,
                                item.CurvePlan.LayerPath);
                            if (!curveLayer.Success)
                            {
                                RollBackCreatedObjects(document, createdIds);
                                return OperationResponse<PanelCladdingSpawnResult>.Fail(curveLayer.Message);
                            }
                            curveLayerIndex = curveLayer.Data;
                            curveLayers[item.CurvePlan.LayerPath] = curveLayerIndex;
                        }
                        var attributes = new ObjectAttributes
                        {
                            Name = item.CurvePlan.Code,
                            LayerIndex = curveLayerIndex,
                            ObjectColor = System.Drawing.Color.FromArgb(
                                item.CurvePlan.ObjectColor.Red,
                                item.CurvePlan.ObjectColor.Green,
                                item.CurvePlan.ObjectColor.Blue),
                            ColorSource = ObjectColorSource.ColorFromObject
                        };
                        foreach ((string key, string value) in item.CurvePlan.UserTextWrites)
                        {
                            attributes.SetUserString(key, value);
                        }
                        Guid createdId = document.Objects.AddCurve(item.Geometry, attributes);
                        if (createdId == Guid.Empty)
                        {
                            RollBackCreatedObjects(document, createdIds);
                            return OperationResponse<PanelCladdingSpawnResult>.Fail(
                                $"PANEL_CLADDING_CURVE_CREATE_FAILED: {item.CurvePlan.Code}");
                        }
                        createdIds.Add(createdId);
                        curveIds.Add(createdId);
                    }
                }

                document.Views.Redraw();
                return OperationResponse<PanelCladdingSpawnResult>.Ok(new PanelCladdingSpawnResult
                {
                    SourcePanelIds = sourcePanelIds,
                    CreatedObjectIds = createdIds.ToArray(),
                    CreatedSurfaceIds = surfaceIds.ToArray(),
                    CreatedCurveIds = curveIds.ToArray(),
                    Cids = preparedRegions.Select(item => item.Region.Cid)
                        .Concat(preparedCurves.Select(item => item.CurvePlan.Cid))
                        .ToArray()
                });
            }
            catch (Exception ex)
            {
                RollBackCreatedObjects(document, createdIds);
                return OperationResponse<PanelCladdingSpawnResult>.Fail(
                    $"PANEL_CLADDING_SPAWN_FAILED: {ex.Message}");
            }
            finally
            {
                if (undoRecord != 0U)
                {
                    document.EndUndoRecord(undoRecord);
                }
            }
        }
        finally
        {
            foreach (PreparedRegion item in preparedRegions)
            {
                item.Geometry.Dispose();
            }
            foreach (PreparedCurve item in preparedCurves)
            {
                item.Geometry.Dispose();
            }
        }
    }

    internal OperationResponse<PreparedPanel> PreparePanel(
        RhinoDoc document,
        string filePath,
        Guid objectId,
        PanelCladdingObjectScope scope,
        bool allowEmptySurfacePlan = false)
    {
        RhinoObject? sourceObject = document.Objects.FindId(objectId);
        if (sourceObject?.Geometry is not Brep sourceBrep)
        {
            return OperationResponse<PreparedPanel>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<PanelCladdingLayout> layoutResponse = _layoutRepository.ReadLayout(filePath, objectId);
        if (!layoutResponse.Success || layoutResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(layoutResponse.Message);
        }
        PanelCladdingLayout layout = layoutResponse.Data;
        if (!layout.CanSave)
        {
            return OperationResponse<PreparedPanel>.Fail(
                $"PANEL_CLADDING_UNSUPPORTED_PROJECTION: {layout.GeometryDiagnostic}");
        }

        IReadOnlyDictionary<string, string> userText = ReadUserText(sourceObject);
        OperationResponse<PanelCladdingKeySet> keySetResponse = _keys.Parse(
            userText,
            layout.Width,
            layout.Height,
            layout.ModelTolerance);
        if (!keySetResponse.Success || keySetResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(keySetResponse.Message);
        }
        PanelCladdingKeySet keySet = keySetResponse.Data;

        PanelCladdingSpawnPlan plan;
        if (scope == PanelCladdingObjectScope.Surfaces)
        {
            OperationResponse<PanelCladdingSpawnPlan> planResponse = allowEmptySurfacePlan
                ? _planning.CreateUpdatePlan(userText, keySet)
                : _planning.CreatePlan(userText, keySet);
            if (!planResponse.Success || planResponse.Data is null)
            {
                return OperationResponse<PreparedPanel>.Fail(planResponse.Message);
            }
            plan = planResponse.Data;
        }
        else
        {
            string pid = GetRequiredUserText(
                userText,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            string cid = GetRequiredUserText(
                userText,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            if (pid.Length == 0 || cid.Length == 0)
            {
                return OperationResponse<PreparedPanel>.Fail(
                    "PANEL_CLADDING_EXTRUSION_PID_CID_REQUIRED");
            }
            OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>> curvePlan =
                _extrusionPlanning.CreatePlan(
                    pid,
                    cid,
                    layout.Width,
                    layout.Height,
                    keySet,
                    layout.LayerFullPath);
            if (!curvePlan.Success || curvePlan.Data is null)
            {
                return OperationResponse<PreparedPanel>.Fail(curvePlan.Message);
            }
            plan = new PanelCladdingSpawnPlan
            {
                PanelId = pid,
                Curves = curvePlan.Data
            };
        }

        OperationResponse<LivePanelCladdingGeometryGrid> gridResponse =
            LivePanelCladdingGeometryPartitionService.CreateGrid(
                sourceBrep,
                keySet,
                layout.ModelTolerance);
        if (!gridResponse.Success || gridResponse.Data is null)
        {
            return OperationResponse<PreparedPanel>.Fail(gridResponse.Message);
        }
        using LivePanelCladdingGeometryGrid grid = gridResponse.Data;
        var prepared = new List<PreparedRegion>(plan.Regions.Count);
        foreach (PanelCladdingSpawnRegionPlan region in plan.Regions)
        {
            OperationResponse<Brep> geometry = LivePanelCladdingGeometryPartitionService.CreateRegionSurface(
                grid,
                region.Cells,
                layout.ModelTolerance,
                region.Cid);
            if (!geometry.Success || geometry.Data is null)
            {
                foreach (PreparedRegion item in prepared)
                {
                    item.Geometry.Dispose();
                }
                return OperationResponse<PreparedPanel>.Fail(geometry.Message);
            }
            prepared.Add(new PreparedRegion(objectId, region, geometry.Data));
        }
        var curves = new List<PreparedCurve>(plan.Curves.Count);
        foreach (PanelCladdingExtrusionCurvePlan curvePlan in plan.Curves)
        {
            OperationResponse<Curve> geometry =
                LivePanelCladdingGeometryPartitionService.CreateExtrusionCurve(grid, curvePlan);
            if (!geometry.Success || geometry.Data is null)
            {
                foreach (PreparedRegion item in prepared)
                {
                    item.Geometry.Dispose();
                }
                foreach (PreparedCurve item in curves)
                {
                    item.Geometry.Dispose();
                }
                return OperationResponse<PreparedPanel>.Fail(geometry.Message);
            }
            curves.Add(new PreparedCurve(objectId, curvePlan, geometry.Data));
        }
        return OperationResponse<PreparedPanel>.Ok(new PreparedPanel(prepared, curves));
    }

    internal static OperationResponse<int> EnsureLayer(RhinoDoc document, string layerPath)
    {
        int layerIndex = document.Layers.FindByFullPath(layerPath, -1);
        if (layerIndex < 0)
        {
            layerIndex = document.Layers.AddPath(layerPath);
        }
        return layerIndex < 0
            ? OperationResponse<int>.Fail($"PANEL_CLADDING_LAYER_CREATE_FAILED: {layerPath}")
            : OperationResponse<int>.Ok(layerIndex);
    }

    internal static OperationResponse<int> EnsureMaterialLayer(
        RhinoDoc document,
        PanelCladdingSpawnRegionPlan cell)
    {
        int layerIndex = document.Layers.FindByFullPath(cell.LayerPath, -1);
        if (layerIndex < 0)
        {
            layerIndex = document.Layers.AddPath(cell.LayerPath);
        }
        if (layerIndex < 0)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_CREATE_FAILED: {cell.LayerPath}");
        }

        Layer? layer = document.Layers[layerIndex];
        if (layer is null)
        {
            return OperationResponse<int>.Fail(
                $"PANEL_CLADDING_LAYER_NOT_FOUND_AFTER_CREATE: {cell.LayerPath}");
        }
        var expectedColor = System.Drawing.Color.FromArgb(
            cell.LayerColor.Red,
            cell.LayerColor.Green,
            cell.LayerColor.Blue);
        if (layer.Color.ToArgb() != expectedColor.ToArgb())
        {
            layer.Color = expectedColor;
            if (!document.Layers.Modify(layer, layerIndex, quiet: true))
            {
                return OperationResponse<int>.Fail(
                    $"PANEL_CLADDING_LAYER_COLOR_FAILED: {cell.LayerPath}");
            }
        }
        return OperationResponse<int>.Ok(layerIndex);
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(RhinoObject rhinoObject)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var strings = rhinoObject.Attributes.GetUserStrings();
        if (strings?.AllKeys is null)
        {
            return result;
        }
        foreach (string? key in strings.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = strings[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static bool IsManagedCidConflict(
        RhinoDoc document,
        RhinoObject rhinoObject,
        PanelCladdingObjectScope scope)
    {
        string layerPath = rhinoObject.Attributes.LayerIndex >= 0
            ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
            : string.Empty;
        return scope switch
        {
            PanelCladdingObjectScope.Surfaces => rhinoObject.Geometry is Brep &&
                PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(layerPath),
            PanelCladdingObjectScope.Curves => rhinoObject.Geometry is Curve &&
                PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layerPath),
            _ => false
        };
    }

    private static string GetRequiredUserText(
        IReadOnlyDictionary<string, string> userText,
        string key) => userText.TryGetValue(key, out string? value)
            ? (value ?? string.Empty).Trim()
            : string.Empty;

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }
        try
        {
            if (!string.Equals(
                Path.GetFullPath(document.Path).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {ex.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private static void RollBackCreatedObjects(RhinoDoc document, IEnumerable<Guid> objectIds)
    {
        foreach (Guid objectId in objectIds.Reverse())
        {
            document.Objects.Delete(objectId, quiet: true);
        }
    }

    internal sealed record PreparedRegion(
        Guid SourcePanelId,
        PanelCladdingSpawnRegionPlan Region,
        Brep Geometry);

    internal sealed record PreparedCurve(
        Guid SourcePanelId,
        PanelCladdingExtrusionCurvePlan CurvePlan,
        Curve Geometry);

    internal sealed record PreparedPanel(
        IReadOnlyList<PreparedRegion> Regions,
        IReadOnlyList<PreparedCurve> Curves);

}
