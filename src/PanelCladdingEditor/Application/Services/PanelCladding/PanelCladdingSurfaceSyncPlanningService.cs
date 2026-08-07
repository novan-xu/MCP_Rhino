using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSurfaceSyncPlanningService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingSurfaceSyncPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingSurfaceSyncPlan> CreatePlan(
        PanelCladdingSurfaceSyncSnapshot snapshot)
    {
        PanelCladdingSurfaceSyncPanelSnapshot[] panels = (snapshot.Panels ??
                Array.Empty<PanelCladdingSurfaceSyncPanelSnapshot>())
            .Where(panel => panel.ObjectId != Guid.Empty)
            .GroupBy(panel => panel.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (panels.Length == 0)
        {
            return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                "PANEL_CLADDING_SURFACE_SYNC_SELECTION_REQUIRED");
        }

        foreach (PanelCladdingSurfaceSyncPanelSnapshot panel in panels)
        {
            if (string.IsNullOrWhiteSpace(panel.PanelId))
            {
                return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_PID_REQUIRED: {panel.ObjectId:D}: expected {PanelCladdingSpawnPlanningService.PanelIdUserTextKey}.");
            }
            if (panel.Layout.ObjectId != panel.ObjectId)
            {
                return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_LAYOUT_MISMATCH: {panel.ObjectId:D}");
            }
            if (!panel.Layout.CanSave)
            {
                return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_UNSUPPORTED_PROJECTION: {panel.ObjectId:D}: {panel.Layout.GeometryDiagnostic}");
            }
            if (panel.Layout.Cells.Count == 0)
            {
                return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_CELLS_REQUIRED: {panel.ObjectId:D}");
            }
        }

        string? duplicatePid = panels
            .GroupBy(panel => panel.PanelId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();
        if (duplicatePid is not null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_DUPLICATE_SELECTED_PID: {duplicatePid}");
        }

        PanelCladdingSurfaceSyncSurfaceSnapshot[] surfaces = (snapshot.Surfaces ??
                Array.Empty<PanelCladdingSurfaceSyncSurfaceSnapshot>())
            .Where(surface => surface.ObjectId != Guid.Empty && !string.IsNullOrWhiteSpace(surface.Cid))
            .ToArray();
        var surfacesByCid = surfaces
            .GroupBy(surface => surface.Cid.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var panelPlans = new List<PanelCladdingSurfaceSyncPanelPlan>(panels.Length);
        var surfacePlans = new List<PanelCladdingSurfaceSyncSurfacePlan>();
        foreach (PanelCladdingSurfaceSyncPanelSnapshot panel in panels)
        {
            string pid = panel.PanelId.Trim();
            PanelCladdingCell[] orderedCells = panel.Layout.Cells
                .OrderBy(cell => cell.Column)
                .ThenBy(cell => cell.Row)
                .ToArray();
            var expectedCids = orderedCells
                .Select(cell => PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, cell.ShortLabel))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            PanelCladdingSurfaceSyncSurfaceSnapshot? unexpected = surfaces.FirstOrDefault(surface =>
                string.Equals(surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase) &&
                !expectedCids.Contains(surface.Cid.Trim()));
            if (unexpected is not null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_UNEXPECTED_CID: {pid}: {unexpected.Cid.Trim()}");
            }

            bool panelChanged = false;
            var cellValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (PanelCladdingCell cell in orderedCells)
            {
                string expectedCid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                    pid,
                    cell.ShortLabel);
                if (!surfacesByCid.TryGetValue(expectedCid, out PanelCladdingSurfaceSyncSurfaceSnapshot[]? matches))
                {
                    return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                        $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {expectedCid}");
                }
                if (matches.Length != 1)
                {
                    return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                        $"PANEL_CLADDING_SURFACE_SYNC_DUPLICATE_CID: {expectedCid}: {matches.Length} surfaces");
                }

                PanelCladdingSurfaceSyncSurfaceSnapshot surface = matches[0];
                if (!string.Equals(surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                        $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_PID_MISMATCH: {expectedCid}: expected {pid}, found {surface.PanelId.Trim()}");
                }

                OperationResponse<string> material = ResolveLayerMaterial(surface.LayerPath, expectedCid);
                if (!material.Success || material.Data is null)
                {
                    return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(material.Message);
                }

                string materialCode = material.Data;
                string currentPanelValue = _keys.NormalizeCladdingValue(cell.Value);
                panelChanged |= !string.Equals(
                    currentPanelValue,
                    materialCode,
                    StringComparison.Ordinal);
                cellValues[cell.UserTextKey] = materialCode;
                surfacePlans.Add(new PanelCladdingSurfaceSyncSurfacePlan
                {
                    ObjectId = surface.ObjectId,
                    PanelObjectId = panel.ObjectId,
                    PanelId = pid,
                    Cid = expectedCid,
                    CellKey = cell.UserTextKey,
                    ExpectedLayerPath = surface.LayerPath,
                    MaterialCode = materialCode,
                    CladdingKeyChanged = !string.Equals(
                        surface.CladdingValue.Trim(),
                        materialCode,
                        StringComparison.Ordinal)
                });
            }

            panelPlans.Add(new PanelCladdingSurfaceSyncPanelPlan
            {
                ObjectId = panel.ObjectId,
                PanelId = pid,
                Layout = panel.Layout,
                CellValues = cellValues,
                CladdingChanged = panelChanged
            });
        }

        return OperationResponse<PanelCladdingSurfaceSyncPlan>.Ok(new PanelCladdingSurfaceSyncPlan
        {
            Panels = panelPlans,
            Surfaces = surfacePlans
        });
    }

    private static OperationResponse<string> ResolveLayerMaterial(string layerPath, string cid)
    {
        string[] segments = (layerPath ?? string.Empty).Split(
            new[] { "::" },
            StringSplitOptions.None);
        if (segments.Length != 3 ||
            !string.Equals(
                segments[0].Trim(),
                PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer,
                StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_LAYER_INVALID: {cid}: expected 02_Material Surfaces::<family>::<material>, found '{layerPath}'.");
        }

        string material = (segments[2] ?? string.Empty).Trim().ToUpperInvariant();
        if (!IsValidMaterialSegment(material))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_MATERIAL_INVALID: {cid}: '{segments[2]}'.");
        }
        string expectedFamily = PanelCladdingSpawnPlanningService.ResolveMaterialFamilyLayer(material);
        if (!string.Equals(segments[1].Trim(), expectedFamily, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_MATERIAL_FAMILY_MISMATCH: {cid}: material {material} belongs under {expectedFamily}, found '{segments[1]}'.");
        }
        return OperationResponse<string>.Ok(material);
    }

    private static bool IsValidMaterialSegment(string value)
    {
        return value.Length > 0 &&
            !value.Contains("::", StringComparison.Ordinal) &&
            value.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) < 0;
    }
}
