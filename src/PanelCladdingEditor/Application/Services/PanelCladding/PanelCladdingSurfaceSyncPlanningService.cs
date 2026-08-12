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
        Guid[] selectedPanelIds = (snapshot.SelectedPanelIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        PanelCladdingSurfaceSyncPanelSnapshot[] panels = (snapshot.Panels ??
                Array.Empty<PanelCladdingSurfaceSyncPanelSnapshot>())
            .Where(panel => panel.ObjectId != Guid.Empty)
            .GroupBy(panel => panel.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (selectedPanelIds.Length == 0)
        {
            selectedPanelIds = panels.Select(panel => panel.ObjectId).ToArray();
        }
        if (selectedPanelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingSurfaceSyncPlan>.Fail(
                "PANEL_CLADDING_SURFACE_SYNC_SELECTION_REQUIRED");
        }

        var issues = (snapshot.Issues ?? Array.Empty<PanelCladdingSurfaceSyncIssue>())
            .Where(issue => issue.PanelObjectId != Guid.Empty)
            .ToList();
        var skippedPanelIds = issues
            .Select(issue => issue.PanelObjectId)
            .ToHashSet();
        void AddIssue(Guid objectId, string panelId, string message)
        {
            if (!skippedPanelIds.Add(objectId))
            {
                return;
            }
            issues.Add(new PanelCladdingSurfaceSyncIssue
            {
                PanelObjectId = objectId,
                PanelId = panelId,
                Message = message
            });
        }

        var candidatePanels = new List<PanelCladdingSurfaceSyncPanelSnapshot>(panels.Length);
        foreach (PanelCladdingSurfaceSyncPanelSnapshot panel in panels)
        {
            if (skippedPanelIds.Contains(panel.ObjectId))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(panel.PanelId))
            {
                AddIssue(panel.ObjectId, string.Empty,
                    $"PANEL_CLADDING_SURFACE_SYNC_PID_REQUIRED: {panel.ObjectId:D}: expected {PanelCladdingSpawnPlanningService.PanelIdUserTextKey}.");
                continue;
            }
            if (panel.Layout.ObjectId != panel.ObjectId)
            {
                AddIssue(panel.ObjectId, panel.PanelId.Trim(),
                    $"PANEL_CLADDING_SURFACE_SYNC_LAYOUT_MISMATCH: {panel.ObjectId:D}");
                continue;
            }
            if (!panel.Layout.CanSave)
            {
                AddIssue(panel.ObjectId, panel.PanelId.Trim(),
                    $"PANEL_CLADDING_SURFACE_SYNC_UNSUPPORTED_PROJECTION: {panel.ObjectId:D}: {panel.Layout.GeometryDiagnostic}");
                continue;
            }
            if (panel.Layout.Cells.Count == 0)
            {
                AddIssue(panel.ObjectId, panel.PanelId.Trim(),
                    $"PANEL_CLADDING_SURFACE_SYNC_CELLS_REQUIRED: {panel.ObjectId:D}");
                continue;
            }
            candidatePanels.Add(panel);
        }

        foreach (IGrouping<string, PanelCladdingSurfaceSyncPanelSnapshot> duplicatePidGroup in candidatePanels
            .GroupBy(panel => panel.PanelId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            foreach (PanelCladdingSurfaceSyncPanelSnapshot panel in duplicatePidGroup)
            {
                AddIssue(panel.ObjectId, duplicatePidGroup.Key,
                    $"PANEL_CLADDING_SURFACE_SYNC_DUPLICATE_SELECTED_PID: {duplicatePidGroup.Key}");
            }
        }

        foreach (Guid missingPanelId in selectedPanelIds.Where(objectId =>
            panels.All(panel => panel.ObjectId != objectId) && !skippedPanelIds.Contains(objectId)))
        {
            AddIssue(missingPanelId, string.Empty,
                $"PANEL_CLADDING_SURFACE_SYNC_PANEL_READ_FAILED: {missingPanelId:D}");
        }

        PanelCladdingSurfaceSyncSurfaceSnapshot[] surfaces = (snapshot.Surfaces ??
                Array.Empty<PanelCladdingSurfaceSyncSurfaceSnapshot>())
            .Where(surface => surface.ObjectId != Guid.Empty && !string.IsNullOrWhiteSpace(surface.Cid))
            .ToArray();
        var surfacesByCid = surfaces
            .GroupBy(surface => surface.Cid.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var panelPlans = new List<PanelCladdingSurfaceSyncPanelPlan>(candidatePanels.Count);
        var surfacePlans = new List<PanelCladdingSurfaceSyncSurfacePlan>();
        foreach (PanelCladdingSurfaceSyncPanelSnapshot panel in candidatePanels)
        {
            if (skippedPanelIds.Contains(panel.ObjectId))
            {
                continue;
            }
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
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_UNEXPECTED_CID: {pid}: {unexpected.Cid.Trim()}");
                continue;
            }

            bool panelChanged = false;
            var cellValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var panelSurfacePlans = new List<PanelCladdingSurfaceSyncSurfacePlan>(orderedCells.Length);
            string? panelIssue = null;
            foreach (PanelCladdingCell cell in orderedCells)
            {
                string expectedCid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                    pid,
                    cell.ShortLabel);
                if (!surfacesByCid.TryGetValue(expectedCid, out PanelCladdingSurfaceSyncSurfaceSnapshot[]? matches))
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {expectedCid}";
                    break;
                }
                if (matches.Length != 1)
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_DUPLICATE_CID: {expectedCid}: {matches.Length} surfaces";
                    break;
                }

                PanelCladdingSurfaceSyncSurfaceSnapshot surface = matches[0];
                if (!string.Equals(surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase))
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_PID_MISMATCH: {expectedCid}: expected {pid}, found {surface.PanelId.Trim()}";
                    break;
                }

                OperationResponse<string> material = ResolveLayerMaterial(surface.LayerPath, expectedCid);
                if (!material.Success || material.Data is null)
                {
                    panelIssue = material.Message;
                    break;
                }

                string materialCode = material.Data;
                string currentPanelValue = _keys.NormalizeCladdingValue(cell.Value);
                panelChanged |= !string.Equals(
                    currentPanelValue,
                    materialCode,
                    StringComparison.Ordinal);
                cellValues[cell.UserTextKey] = materialCode;
                panelSurfacePlans.Add(new PanelCladdingSurfaceSyncSurfacePlan
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

            if (panelIssue is not null)
            {
                AddIssue(panel.ObjectId, pid, panelIssue);
                continue;
            }

            surfacePlans.AddRange(panelSurfacePlans);
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
            SelectedPanelIds = selectedPanelIds,
            Panels = panelPlans,
            Surfaces = surfacePlans,
            Issues = issues
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
