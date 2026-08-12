extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
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
        IReadOnlyList<Guid> objectIds)
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

        var prepared = new List<PreparedRegion>();
        try
        {
            foreach (Guid sourcePanelId in sourcePanelIds)
            {
                OperationResponse<IReadOnlyList<PreparedRegion>> panel = PreparePanel(
                    document,
                    filePath,
                    sourcePanelId);
                if (!panel.Success || panel.Data is null)
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_PANEL_PREPARE_FAILED: {sourcePanelId:D}: {panel.Message}");
                }
                prepared.AddRange(panel.Data);
            }

            string? duplicateCid = prepared
                .GroupBy(item => item.Region.Cid, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (duplicateCid is not null)
            {
                return OperationResponse<PanelCladdingSpawnResult>.Fail(
                    $"PANEL_CLADDING_DUPLICATE_BATCH_CID: {duplicateCid}");
            }
            foreach (PreparedRegion item in prepared)
            {
                RhinoObject[]? existing = document.Objects.FindByUserString(
                    PanelCladdingSpawnPlanningService.CidUserTextKey,
                    item.Region.Cid,
                    caseSensitive: false);
                if (existing is { Length: > 0 })
                {
                    return OperationResponse<PanelCladdingSpawnResult>.Fail(
                        $"PANEL_CLADDING_CID_ALREADY_EXISTS: {item.Region.Cid}");
                }
            }

            uint undoRecord = document.BeginUndoRecord("Spawn Panel Cladding");
            var createdIds = new List<Guid>(prepared.Count);
            try
            {
                foreach (PreparedRegion item in prepared)
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
                }

                document.Views.Redraw();
                return OperationResponse<PanelCladdingSpawnResult>.Ok(new PanelCladdingSpawnResult
                {
                    SourcePanelIds = sourcePanelIds,
                    CreatedObjectIds = createdIds.ToArray(),
                    Cids = prepared.Select(item => item.Region.Cid).ToArray()
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
            foreach (PreparedRegion item in prepared)
            {
                item.Geometry.Dispose();
            }
        }
    }

    private OperationResponse<IReadOnlyList<PreparedRegion>> PreparePanel(
        RhinoDoc document,
        string filePath,
        Guid objectId)
    {
        RhinoObject? sourceObject = document.Objects.FindId(objectId);
        if (sourceObject?.Geometry is not Brep sourceBrep)
        {
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail("PANEL_CLADDING_BREP_NOT_FOUND");
        }

        OperationResponse<PanelCladdingLayout> layoutResponse = _layoutRepository.ReadLayout(filePath, objectId);
        if (!layoutResponse.Success || layoutResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(layoutResponse.Message);
        }
        PanelCladdingLayout layout = layoutResponse.Data;
        if (!layout.CanSave)
        {
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(
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
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(keySetResponse.Message);
        }
        PanelCladdingKeySet keySet = keySetResponse.Data;

        OperationResponse<PanelCladdingSpawnPlan> planResponse = _planning.CreatePlan(userText, keySet);
        if (!planResponse.Success || planResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(planResponse.Message);
        }
        PanelCladdingSpawnPlan plan = planResponse.Data;

        OperationResponse<LivePanelCladdingGeometryGrid> gridResponse =
            LivePanelCladdingGeometryPartitionService.CreateGrid(
                sourceBrep,
                keySet,
                layout.ModelTolerance);
        if (!gridResponse.Success || gridResponse.Data is null)
        {
            return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(gridResponse.Message);
        }
        using LivePanelCladdingGeometryGrid grid = gridResponse.Data;
        var prepared = new List<PreparedRegion>(plan.Regions.Count);
        foreach (PanelCladdingSpawnRegionPlan region in plan.Regions)
        {
            OperationResponse<Brep> geometry = LivePanelCladdingGeometryPartitionService.JoinRegion(
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
                return OperationResponse<IReadOnlyList<PreparedRegion>>.Fail(geometry.Message);
            }
            prepared.Add(new PreparedRegion(objectId, region, geometry.Data));
        }
        return OperationResponse<IReadOnlyList<PreparedRegion>>.Ok(prepared);
    }

    private static OperationResponse<int> EnsureMaterialLayer(
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

    private sealed record PreparedRegion(
        Guid SourcePanelId,
        PanelCladdingSpawnRegionPlan Region,
        Brep Geometry);

}
