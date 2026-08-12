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
            .Where(surface => surface.ObjectId != Guid.Empty)
            .GroupBy(surface => surface.ObjectId)
            .Select(group => group.First())
            .ToArray();

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
            var cellsByLabel = orderedCells.ToDictionary(
                cell => cell.ShortLabel,
                StringComparer.OrdinalIgnoreCase);
            var expectedCids = orderedCells
                .Select(cell => PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, cell.ShortLabel))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            PanelCladdingSurfaceSyncSurfaceSnapshot? wrongPid = surfaces.FirstOrDefault(surface =>
                expectedCids.Contains(surface.Cid.Trim()) &&
                !string.Equals(surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase));
            if (wrongPid is not null)
            {
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_PID_MISMATCH: {wrongPid.Cid.Trim()}: expected {pid}, found {wrongPid.PanelId.Trim()}");
                continue;
            }

            PanelCladdingSurfaceSyncSurfaceSnapshot[] panelSurfaces = surfaces
                .Where(surface => string.Equals(
                    surface.PanelId.Trim(),
                    pid,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (panelSurfaces.Length == 0)
            {
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, orderedCells[0].ShortLabel)}");
                continue;
            }
            IGrouping<string, PanelCladdingSurfaceSyncSurfaceSnapshot>? duplicateCid = panelSurfaces
                .Where(surface => !string.IsNullOrWhiteSpace(surface.Cid))
                .GroupBy(surface => surface.Cid.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1 && group.Any(surface =>
                    (surface.CoveredCellLabels ?? Array.Empty<string>()).Count == 0));
            if (duplicateCid is not null)
            {
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_DUPLICATE_CID: {duplicateCid.Key}: {duplicateCid.Count()} surfaces");
                continue;
            }

            var cellValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var claimedCells = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var panelSurfacePlans = new List<PanelCladdingSurfaceSyncSurfacePlan>(panelSurfaces.Length);
            string? panelIssue = null;
            foreach (PanelCladdingSurfaceSyncSurfaceSnapshot surface in panelSurfaces)
            {
                OperationResponse<IReadOnlyList<string>> coverage = ResolveCoverageLabels(
                    surface,
                    pid,
                    cellsByLabel);
                if (!coverage.Success || coverage.Data is null)
                {
                    panelIssue = coverage.Message;
                    break;
                }
                string[] coveredLabels = coverage.Data
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!IsEdgeConnected(coveredLabels, cellsByLabel))
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_REGION_DISCONNECTED: {surface.ObjectId:D}: {string.Join(",", coveredLabels)}";
                    break;
                }
                string? overlap = coveredLabels.FirstOrDefault(claimedCells.ContainsKey);
                if (overlap is not null)
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_CELL_OVERLAP: {overlap}: {claimedCells[overlap]:D}, {surface.ObjectId:D}";
                    break;
                }

                string surfaceIdentity = string.IsNullOrWhiteSpace(surface.Cid)
                    ? surface.ObjectId.ToString("D")
                    : surface.Cid.Trim();
                OperationResponse<string> material = ResolveLayerMaterial(surface.LayerPath, surfaceIdentity);
                if (!material.Success || material.Data is null)
                {
                    panelIssue = material.Message;
                    break;
                }

                string materialCode = material.Data;
                PanelCladdingCell owner = coveredLabels
                    .Select(label => cellsByLabel[label])
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .First();
                foreach (string label in coveredLabels)
                {
                    PanelCladdingCell cell = cellsByLabel[label];
                    claimedCells[label] = surface.ObjectId;
                    cellValues[cell.UserTextKey] = string.Equals(
                        label,
                        owner.ShortLabel,
                        StringComparison.OrdinalIgnoreCase)
                        ? materialCode
                        : owner.ShortLabel;
                }
                string desiredCid = PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                    pid,
                    owner.ShortLabel);
                panelSurfacePlans.Add(new PanelCladdingSurfaceSyncSurfacePlan
                {
                    ObjectId = surface.ObjectId,
                    PanelObjectId = panel.ObjectId,
                    PanelId = pid,
                    ExpectedCid = surface.Cid.Trim(),
                    DesiredCid = desiredCid,
                    CellKey = owner.UserTextKey,
                    CoveredCellLabels = coveredLabels,
                    ExpectedLayerPath = surface.LayerPath,
                    MaterialCode = materialCode,
                    CladdingKeyChanged = !string.Equals(
                        surface.CladdingValue.Trim(),
                        materialCode,
                        StringComparison.Ordinal),
                    CidChanged = !string.Equals(
                        surface.Cid.Trim(),
                        desiredCid,
                        StringComparison.Ordinal)
                });
            }

            PanelCladdingCell? uncovered = orderedCells.FirstOrDefault(cell =>
                !claimedCells.ContainsKey(cell.ShortLabel));
            if (panelIssue is null && uncovered is not null)
            {
                panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, uncovered.ShortLabel)}";
            }
            if (panelIssue is not null)
            {
                AddIssue(panel.ObjectId, pid, panelIssue);
                continue;
            }

            bool panelChanged = panel.GridChanged || orderedCells.Any(cell => !string.Equals(
                _keys.NormalizeCladdingValue(cell.Value),
                cellValues[cell.UserTextKey],
                StringComparison.Ordinal));
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

    private static OperationResponse<IReadOnlyList<string>> ResolveCoverageLabels(
        PanelCladdingSurfaceSyncSurfaceSnapshot surface,
        string panelId,
        IReadOnlyDictionary<string, PanelCladdingCell> cellsByLabel)
    {
        string[] supplied = (surface.CoveredCellLabels ?? Array.Empty<string>())
            .Select(label => (label ?? string.Empty).Trim().ToUpperInvariant())
            .Where(label => label.Length > 0)
            .ToArray();
        if (supplied.Length > 0)
        {
            string? unknown = supplied.FirstOrDefault(label => !cellsByLabel.ContainsKey(label));
            return unknown is null
                ? OperationResponse<IReadOnlyList<string>>.Ok(supplied)
                : OperationResponse<IReadOnlyList<string>>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_COVERAGE_CELL_UNKNOWN: {surface.ObjectId:D}: {unknown}");
        }

        PanelCladdingCell? legacyCell = cellsByLabel.Values.FirstOrDefault(cell => string.Equals(
            PanelCladdingSpawnPlanningService.BuildSurfaceCid(panelId, cell.ShortLabel),
            surface.Cid.Trim(),
            StringComparison.OrdinalIgnoreCase));
        if (legacyCell is not null)
        {
            return OperationResponse<IReadOnlyList<string>>.Ok(new[] { legacyCell.ShortLabel });
        }
        return OperationResponse<IReadOnlyList<string>>.Fail(
            $"PANEL_CLADDING_SURFACE_SYNC_UNEXPECTED_CID: {panelId}: {surface.Cid.Trim()}");
    }

    private static bool IsEdgeConnected(
        IReadOnlyList<string> labels,
        IReadOnlyDictionary<string, PanelCladdingCell> cellsByLabel)
    {
        var coordinates = labels
            .Select(label => cellsByLabel[label])
            .Select(cell => (cell.Column, cell.Row))
            .ToHashSet();
        if (coordinates.Count <= 1)
        {
            return true;
        }
        var visited = new HashSet<(int Column, int Row)>();
        var pending = new Queue<(int Column, int Row)>();
        pending.Enqueue(coordinates.First());
        while (pending.Count > 0)
        {
            (int column, int row) = pending.Dequeue();
            if (!visited.Add((column, row)))
            {
                continue;
            }
            foreach ((int nextColumn, int nextRow) in new[]
            {
                (column - 1, row), (column + 1, row),
                (column, row - 1), (column, row + 1)
            })
            {
                if (coordinates.Contains((nextColumn, nextRow)) &&
                    !visited.Contains((nextColumn, nextRow)))
                {
                    pending.Enqueue((nextColumn, nextRow));
                }
            }
        }
        return visited.Count == coordinates.Count;
    }

    private static OperationResponse<string> ResolveLayerMaterial(string layerPath, string cid)
    {
        string[] segments = (layerPath ?? string.Empty).Split(
            new[] { "::" },
            StringSplitOptions.None);
        if (segments.Length != 3 ||
            !PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot(segments[0]))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_LAYER_INVALID: {cid}: expected " +
                $"{PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}::<family>::<material>, found '{layerPath}'.");
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
