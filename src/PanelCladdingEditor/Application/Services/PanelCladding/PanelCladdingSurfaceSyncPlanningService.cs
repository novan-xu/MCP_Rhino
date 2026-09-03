using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSurfaceSyncPlanningService
{
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingLogicService _claddingLogic;
    private readonly PanelCladdingSurfaceCoverageService _surfaceCoverage;

    public PanelCladdingSurfaceSyncPlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
        _claddingLogic = new PanelCladdingLogicService();
        _surfaceCoverage = new PanelCladdingSurfaceCoverageService();
    }

    public static OperationResponse<IReadOnlyList<double>> CombineSurfaceScopeOffsets(
        IReadOnlyList<double> existingOffsets,
        IReadOnlyList<double> inferredOffsets,
        double extent,
        double modelTolerance,
        string axisLabel)
    {
        if (!double.IsFinite(extent) || extent <= 0d ||
            !double.IsFinite(modelTolerance) || modelTolerance <= 0d)
        {
            return OperationResponse<IReadOnlyList<double>>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_{axisLabel}_EXTENT_INVALID");
        }

        double tolerance = Math.Max(modelTolerance * 10d, extent * 1e-7d);
        var combined = new List<double>();
        OperationResponse append(IReadOnlyList<double> values)
        {
            foreach (double value in (values ?? Array.Empty<double>()).OrderBy(value => value))
            {
                if (!double.IsFinite(value) ||
                    value <= tolerance ||
                    value >= extent - tolerance)
                {
                    return OperationResponse.Fail(
                        $"PANEL_CLADDING_SURFACE_SYNC_{axisLabel}_OFFSET_INVALID: {value}");
                }
                if (!combined.Any(existing => Math.Abs(value - existing) <= tolerance))
                {
                    combined.Add(value);
                }
            }
            return OperationResponse.Ok();
        }
        OperationResponse existing = append(existingOffsets);
        if (!existing.Success)
        {
            return OperationResponse<IReadOnlyList<double>>.Fail(existing.Message);
        }
        OperationResponse inferred = append(inferredOffsets);
        if (!inferred.Success)
        {
            return OperationResponse<IReadOnlyList<double>>.Fail(inferred.Message);
        }
        combined.Sort();
        return OperationResponse<IReadOnlyList<double>>.Ok(combined);
    }

    public static OperationResponse<PanelCladdingInferredOffsets> ResolveEffectiveOffsets(
        PanelCladdingObjectScope scope,
        IReadOnlyList<double> existingHorizontalOffsets,
        IReadOnlyList<double> existingVerticalOffsets,
        PanelCladdingInferredOffsets inferred,
        double panelWidth,
        double panelHeight,
        double modelTolerance)
    {
        if (scope == PanelCladdingObjectScope.Curves)
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Ok(inferred);
        }

        return ResolveSurfaceScopeOffsets(
            existingHorizontalOffsets,
            existingVerticalOffsets,
            inferred,
            curveInferred: null,
            panelWidth,
            panelHeight,
            modelTolerance);
    }

    public static OperationResponse<PanelCladdingInferredOffsets> ResolveSurfaceScopeOffsets(
        IReadOnlyList<double> existingHorizontalOffsets,
        IReadOnlyList<double> existingVerticalOffsets,
        PanelCladdingInferredOffsets surfaceInferred,
        PanelCladdingInferredOffsets? curveInferred,
        double panelWidth,
        double panelHeight,
        double modelTolerance)
    {
        OperationResponse<IReadOnlyList<double>> horizontal = ResolveAxis(
            existingHorizontalOffsets,
            surfaceInferred.HorizontalOffsets,
            curveInferred?.HorizontalOffsets,
            new HashSet<int>(),
            panelHeight,
            modelTolerance,
            "HORIZONTAL");
        OperationResponse<IReadOnlyList<double>> vertical = ResolveAxis(
            existingVerticalOffsets,
            surfaceInferred.VerticalOffsets,
            curveInferred?.VerticalOffsets,
            new HashSet<int>(),
            panelWidth,
            modelTolerance,
            "VERTICAL");
        return BuildResolvedOffsets(horizontal, vertical);
    }

    public static OperationResponse<PanelCladdingInferredOffsets> ResolveSurfaceScopeOffsets(
        PanelCladdingLayout existingLayout,
        PanelCladdingInferredOffsets surfaceInferred,
        PanelCladdingInferredOffsets? curveInferred,
        IReadOnlyList<IReadOnlyList<string>>? storedSurfaceCoverages = null)
    {
        ArgumentNullException.ThrowIfNull(existingLayout);
        ArgumentNullException.ThrowIfNull(surfaceInferred);

        OperationResponse<NonSplittingTrackIndexes> tracks = ResolveNonSplittingTrackIndexes(
            existingLayout,
            storedSurfaceCoverages);
        if (!tracks.Success || tracks.Data is null)
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Fail(tracks.Message);
        }
        OperationResponse<IReadOnlyList<double>> horizontal = ResolveAxis(
            existingLayout.HorizontalOffsets,
            surfaceInferred.HorizontalOffsets,
            curveInferred?.HorizontalOffsets,
            tracks.Data.Horizontal,
            existingLayout.Height,
            existingLayout.ModelTolerance,
            "HORIZONTAL");
        OperationResponse<IReadOnlyList<double>> vertical = ResolveAxis(
            existingLayout.VerticalOffsets,
            surfaceInferred.VerticalOffsets,
            curveInferred?.VerticalOffsets,
            tracks.Data.Vertical,
            existingLayout.Width,
            existingLayout.ModelTolerance,
            "VERTICAL");
        return BuildResolvedOffsets(horizontal, vertical);
    }

    private static OperationResponse<PanelCladdingInferredOffsets> BuildResolvedOffsets(
        OperationResponse<IReadOnlyList<double>> horizontal,
        OperationResponse<IReadOnlyList<double>> vertical)
    {
        if (!horizontal.Success || horizontal.Data is null ||
            !vertical.Success || vertical.Data is null)
        {
            return OperationResponse<PanelCladdingInferredOffsets>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_STRUCTURAL_GRID_INVALID: " +
                $"{horizontal.Message}{vertical.Message}");
        }
        return OperationResponse<PanelCladdingInferredOffsets>.Ok(
            new PanelCladdingInferredOffsets
            {
                HorizontalOffsets = horizontal.Data,
                VerticalOffsets = vertical.Data
            });
    }

    private static OperationResponse<IReadOnlyList<double>> ResolveAxis(
        IReadOnlyList<double> stored,
        IReadOnlyList<double> surfaces,
        IReadOnlyList<double>? curves,
        IReadOnlySet<int> nonSplittingStoredTracks,
        double extent,
        double tolerance,
        string label)
    {
        var structural = nonSplittingStoredTracks
            .Where(index => index >= 0 && index < stored.Count)
            .OrderBy(index => index)
            .Select(index => stored[index])
            .ToList();
        if (curves is not null && curves.Count > 0)
        {
            OperationResponse<PanelCladdingAxisCorrespondence> aligned =
                PanelCladdingLayoutReconciliationService.AlignAxis(
                    stored,
                    curves,
                    extent,
                    tolerance,
                    label);
            if (!aligned.Success || aligned.Data is null)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(aligned.Message);
            }
            structural.Clear();
            foreach (int oldIndex in nonSplittingStoredTracks
                .Where(index => index >= 0 && index < stored.Count)
                .OrderBy(index => index))
            {
                int? newIndex = oldIndex < aligned.Data.OldToNewTracks.Count
                    ? aligned.Data.OldToNewTracks[oldIndex]
                    : null;
                structural.Add(newIndex.HasValue
                    ? curves[newIndex.Value]
                    : stored[oldIndex]);
            }
            for (int newIndex = 0; newIndex < curves.Count; newIndex++)
            {
                if (newIndex >= aligned.Data.NewToOldTracks.Count ||
                    !aligned.Data.NewToOldTracks[newIndex].HasValue)
                {
                    structural.Add(curves[newIndex]);
                }
            }
        }
        return CombineSurfaceScopeOffsets(
            surfaces,
            structural,
            extent,
            tolerance,
            label);
    }

    private static OperationResponse<NonSplittingTrackIndexes> ResolveNonSplittingTrackIndexes(
        PanelCladdingLayout layout,
        IReadOnlyList<IReadOnlyList<string>>? storedSurfaceCoverages)
    {
        var cellsByCoordinate = layout.Cells.ToDictionary(
            cell => (cell.Column, cell.Row),
            cell => cell,
            EqualityComparer<(int Column, int Row)>.Default);
        if (cellsByCoordinate.Count != layout.ColumnCount * layout.RowCount)
        {
            return OperationResponse<NonSplittingTrackIndexes>.Fail(
                "PANEL_CLADDING_SURFACE_COVERAGE_LAYOUT_INCOMPLETE");
        }

        if (storedSurfaceCoverages is not null)
        {
            var surfaceByCell = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int surfaceIndex = 0; surfaceIndex < storedSurfaceCoverages.Count; surfaceIndex++)
            {
                IReadOnlyList<string> coverage = storedSurfaceCoverages[surfaceIndex] ??
                    Array.Empty<string>();
                if (coverage.Count == 0)
                {
                    return OperationResponse<NonSplittingTrackIndexes>.Fail(
                        $"PANEL_CLADDING_SURFACE_COVERAGE_EMPTY: {surfaceIndex}");
                }
                foreach (string suppliedLabel in coverage)
                {
                    string label = (suppliedLabel ?? string.Empty).Trim().ToUpperInvariant();
                    if (!layout.Cells.Any(cell => string.Equals(
                            cell.ShortLabel,
                            label,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        return OperationResponse<NonSplittingTrackIndexes>.Fail(
                            $"PANEL_CLADDING_SURFACE_COVERAGE_CELL_UNKNOWN: {label}");
                    }
                    if (!surfaceByCell.TryAdd(label, surfaceIndex))
                    {
                        return OperationResponse<NonSplittingTrackIndexes>.Fail(
                            $"PANEL_CLADDING_SURFACE_COVERAGE_CELL_OVERLAP: {label}");
                    }
                }
            }
            PanelCladdingCell? missing = layout.Cells.FirstOrDefault(cell =>
                !surfaceByCell.ContainsKey(cell.ShortLabel));
            if (missing is not null)
            {
                return OperationResponse<NonSplittingTrackIndexes>.Fail(
                    $"PANEL_CLADDING_SURFACE_COVERAGE_CELL_MISSING: {missing.ShortLabel}");
            }
            return OperationResponse<NonSplittingTrackIndexes>.Ok(BuildTrackIndexes(
                (first, second) => surfaceByCell[first.ShortLabel] == surfaceByCell[second.ShortLabel]));
        }

        return OperationResponse<NonSplittingTrackIndexes>.Ok(
            new NonSplittingTrackIndexes(new HashSet<int>(), new HashSet<int>()));

        NonSplittingTrackIndexes BuildTrackIndexes(
            Func<PanelCladdingCell, PanelCladdingCell, bool> sameSurface)
        {
            IReadOnlySet<int> resolvedHorizontal = Enumerable.Range(0, layout.HorizontalOffsets.Count)
                .Where(track => Enumerable.Range(0, layout.ColumnCount).All(column =>
                    sameSurface(
                        cellsByCoordinate[(column, track)],
                        cellsByCoordinate[(column, track + 1)])))
                .ToHashSet();
            IReadOnlySet<int> resolvedVertical = Enumerable.Range(0, layout.VerticalOffsets.Count)
                .Where(track => Enumerable.Range(0, layout.RowCount).All(row =>
                    sameSurface(
                        cellsByCoordinate[(track, row)],
                        cellsByCoordinate[(track + 1, row)])))
                .ToHashSet();
            return new NonSplittingTrackIndexes(resolvedHorizontal, resolvedVertical);
        }

    }

    private sealed record NonSplittingTrackIndexes(
        IReadOnlySet<int> Horizontal,
        IReadOnlySet<int> Vertical);

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
        var curvePlans = new List<PanelCladdingSurfaceSyncCurvePlan>();
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
            string storedCladdingLogic = GetUserText(
                panel.Layout.SourceUserText,
                PanelCladdingKeyService.CladdingLogicKey);
            OperationResponse<IReadOnlyDictionary<string, string>> decodedCladdingLogic =
                _claddingLogic.Decode(storedCladdingLogic);
            if (!decodedCladdingLogic.Success || decodedCladdingLogic.Data is null)
            {
                AddIssue(panel.ObjectId, panel.PanelId.Trim(),
                    $"PANEL_CLADDING_SURFACE_SYNC_LOGIC_INVALID: {panel.ObjectId:D}: {decodedCladdingLogic.Message}");
                continue;
            }
            IReadOnlyDictionary<string, string> savedCladdingLogic = decodedCladdingLogic.Data;
            if (snapshot.Scope == PanelCladdingObjectScope.Curves)
            {
                PanelCladdingSurfaceSyncCurveSnapshot[] scopedCurves = (snapshot.Curves ??
                        Array.Empty<PanelCladdingSurfaceSyncCurveSnapshot>())
                    .Where(curve => curve.PanelObjectId == panel.ObjectId)
                    .ToArray();
                if (scopedCurves.Length == 0)
                {
                    AddIssue(panel.ObjectId, pid, $"PANEL_CLADDING_SYNC_CURVES_MISSING: {pid}");
                    continue;
                }
                string curvePanelCid = string.IsNullOrWhiteSpace(panel.PanelCid)
                    ? (pid.StartsWith("PID_", StringComparison.OrdinalIgnoreCase) ? "CID_" + pid[4..] : pid)
                    : panel.PanelCid.Trim();
                foreach (PanelCladdingSurfaceSyncCurveSnapshot curve in scopedCurves)
                {
                    string desiredCid = $"{curvePanelCid}-{curve.DesiredCode}";
                    curvePlans.Add(new PanelCladdingSurfaceSyncCurvePlan
                    {
                        ObjectId = curve.ObjectId,
                        PanelObjectId = panel.ObjectId,
                        PanelId = pid,
                        ExpectedLayerPath = curve.LayerPath,
                        DesiredCode = curve.DesiredCode,
                        DesiredCid = desiredCid,
                        DesiredAssignedExtrusions = curve.DesiredAssignedExtrusions,
                        DesiredAssignedExtrusionValues = curve.DesiredAssignedExtrusionValues,
                        DesiredObjectColor = curve.DesiredObjectColor,
                        MetadataChanged = !string.Equals(curve.PanelId.Trim(), pid, StringComparison.Ordinal) ||
                            !string.Equals(curve.Cid.Trim(), desiredCid, StringComparison.Ordinal) ||
                            !string.Equals(curve.CurveCode.Trim(), curve.DesiredCode, StringComparison.Ordinal) ||
                            !string.Equals(
                                curve.AssignedExtrusions.Trim(),
                                curve.DesiredAssignedExtrusions,
                                StringComparison.Ordinal) ||
                            !DictionariesEqual(
                                curve.AssignedExtrusionValues,
                                curve.DesiredAssignedExtrusionValues) ||
                            curve.ObjectColorChanged
                    });
                }
                IReadOnlyDictionary<string, string> curveCellValues = orderedCells.ToDictionary(
                    cell => cell.UserTextKey,
                    cell => _keys.NormalizeCladdingValue(cell.Value),
                    StringComparer.OrdinalIgnoreCase);
                OperationResponse<string> curveCladdingLogic = _claddingLogic.Encode(
                    orderedCells,
                    curveCellValues);
                if (!curveCladdingLogic.Success || curveCladdingLogic.Data is null)
                {
                    AddIssue(panel.ObjectId, pid,
                        $"PANEL_CLADDING_SYNC_LOGIC_INVALID: {panel.ObjectId:D}: {curveCladdingLogic.Message}");
                    continue;
                }
                panelPlans.Add(new PanelCladdingSurfaceSyncPanelPlan
                {
                    ObjectId = panel.ObjectId,
                    PanelId = pid,
                    Layout = panel.Layout,
                    CellValues = curveCellValues,
                    CladdingLogic = curveCladdingLogic.Data,
                    CladdingChanged = panel.GridChanged
                });
                continue;
            }
            PanelCladdingSurfaceSyncSurfaceSnapshot[] panelSurfaces = surfaces
                .Where(surface => surface.PanelObjectId == panel.ObjectId ||
                    (surface.PanelObjectId == Guid.Empty && string.Equals(
                        surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (panelSurfaces.Length == 0)
            {
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, orderedCells[0].ShortLabel)}");
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
                PanelCladdingCell geometryOwner = coveredLabels
                    .Select(label => cellsByLabel[label])
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .First();
                OperationResponse<string> savedOwner = _claddingLogic.ResolveOwnerLabel(
                    coveredLabels,
                    savedCladdingLogic);
                if (!savedOwner.Success || savedOwner.Data is null)
                {
                    panelIssue = $"PANEL_CLADDING_SURFACE_SYNC_LOGIC_INVALID: " +
                        $"{panel.ObjectId:D}: {savedOwner.Message}";
                    break;
                }
                PanelCladdingCell owner = savedOwner.Data.Length > 0
                    ? cellsByLabel[savedOwner.Data]
                    : geometryOwner;
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
                OperationResponse<string> desiredCoverage = _surfaceCoverage.Encode(
                    owner.ShortLabel,
                    coveredLabels.Select(label => cellsByLabel[label]));
                if (!desiredCoverage.Success || desiredCoverage.Data is null)
                {
                    panelIssue = desiredCoverage.Message;
                    break;
                }
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
                    DesiredCoverageValue = desiredCoverage.Data,
                    CladdingKeyChanged = !string.Equals(
                        surface.CladdingValue.Trim(),
                        materialCode,
                        StringComparison.Ordinal),
                    CoverageChanged = !string.Equals(
                        surface.CoverageValue,
                        desiredCoverage.Data,
                        StringComparison.Ordinal) ||
                        !string.IsNullOrWhiteSpace(surface.LegacyCoverageValue),
                    CidChanged = !string.Equals(
                        surface.Cid.Trim(),
                        desiredCid,
                        StringComparison.Ordinal),
                    PidChanged = !string.Equals(
                        surface.PanelId.Trim(),
                        pid,
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

            OperationResponse<string> desiredCladdingLogic = _claddingLogic.Encode(
                orderedCells,
                cellValues);
            if (!desiredCladdingLogic.Success || desiredCladdingLogic.Data is null)
            {
                AddIssue(panel.ObjectId, pid,
                    $"PANEL_CLADDING_SURFACE_SYNC_LOGIC_INVALID: {panel.ObjectId:D}: {desiredCladdingLogic.Message}");
                continue;
            }
            bool panelChanged = panel.GridChanged ||
                !string.Equals(
                    storedCladdingLogic,
                    desiredCladdingLogic.Data,
                    StringComparison.Ordinal) ||
                orderedCells.Any(cell => !string.Equals(
                _keys.NormalizeCladdingValue(cell.Value),
                cellValues[cell.UserTextKey],
                StringComparison.Ordinal));
            PanelCladdingSurfaceSyncCurveSnapshot[] panelCurves = (snapshot.Curves ??
                    Array.Empty<PanelCladdingSurfaceSyncCurveSnapshot>())
                .Where(curve => curve.PanelObjectId == panel.ObjectId)
                .ToArray();
            surfacePlans.AddRange(panelSurfacePlans);
            string panelCid = string.IsNullOrWhiteSpace(panel.PanelCid)
                ? (pid.StartsWith("PID_", StringComparison.OrdinalIgnoreCase) ? "CID_" + pid[4..] : pid)
                : panel.PanelCid.Trim();
            foreach (PanelCladdingSurfaceSyncCurveSnapshot curve in panelCurves)
            {
                string desiredCid = $"{panelCid}-{curve.DesiredCode}";
                curvePlans.Add(new PanelCladdingSurfaceSyncCurvePlan
                {
                    ObjectId = curve.ObjectId,
                    PanelObjectId = panel.ObjectId,
                    PanelId = pid,
                    ExpectedLayerPath = curve.LayerPath,
                    DesiredCode = curve.DesiredCode,
                    DesiredCid = desiredCid,
                    DesiredAssignedExtrusions = curve.DesiredAssignedExtrusions,
                    DesiredAssignedExtrusionValues = curve.DesiredAssignedExtrusionValues,
                    DesiredObjectColor = curve.DesiredObjectColor,
                    MetadataChanged = !string.Equals(curve.PanelId.Trim(), pid, StringComparison.Ordinal) ||
                        !string.Equals(curve.Cid.Trim(), desiredCid, StringComparison.Ordinal) ||
                        !string.Equals(curve.CurveCode.Trim(), curve.DesiredCode, StringComparison.Ordinal) ||
                        !string.Equals(
                            curve.AssignedExtrusions.Trim(),
                            curve.DesiredAssignedExtrusions,
                            StringComparison.Ordinal) ||
                        !DictionariesEqual(
                            curve.AssignedExtrusionValues,
                            curve.DesiredAssignedExtrusionValues) ||
                        curve.ObjectColorChanged
                });
            }
            panelPlans.Add(new PanelCladdingSurfaceSyncPanelPlan
            {
                ObjectId = panel.ObjectId,
                PanelId = pid,
                Layout = panel.Layout,
                CellValues = cellValues,
                CladdingLogic = desiredCladdingLogic.Data,
                CladdingChanged = panelChanged
            });
        }

        return OperationResponse<PanelCladdingSurfaceSyncPlan>.Ok(new PanelCladdingSurfaceSyncPlan
        {
            Scope = snapshot.Scope,
            SelectedPanelIds = selectedPanelIds,
            Panels = panelPlans,
            Surfaces = surfacePlans,
            Curves = curvePlans,
            Issues = issues
        });
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }
        return left.All(item => right.TryGetValue(item.Key, out string? value) &&
                                string.Equals(item.Value, value, StringComparison.Ordinal));
    }

    private static string GetUserText(
        IReadOnlyDictionary<string, string> userText,
        string requestedKey) =>
        userText.FirstOrDefault(item => string.Equals(
            item.Key,
            requestedKey,
            StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

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
