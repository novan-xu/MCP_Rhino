using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingLayoutReconciliationService
{
    private const double InsertDeleteCost = 0.30d;
    private const double CostEpsilon = 1e-12d;

    public OperationResponse<PanelCladdingLayoutReconciliationResult> Reconcile(
        IReadOnlyList<double> oldHorizontalOffsets,
        IReadOnlyList<double> oldVerticalOffsets,
        IReadOnlyList<double> newHorizontalOffsets,
        IReadOnlyList<double> newVerticalOffsets,
        PanelCladdingTopologyState oldTopology,
        double panelWidth,
        double panelHeight,
        double modelTolerance)
    {
        if (!double.IsFinite(panelWidth) || panelWidth <= 0d ||
            !double.IsFinite(panelHeight) || panelHeight <= 0d ||
            !double.IsFinite(modelTolerance) || modelTolerance <= 0d)
        {
            return OperationResponse<PanelCladdingLayoutReconciliationResult>.Fail(
                "PANEL_CLADDING_RECONCILIATION_EXTENT_INVALID");
        }

        OperationResponse<PanelCladdingAxisCorrespondence> horizontal = AlignAxis(
            oldHorizontalOffsets,
            newHorizontalOffsets,
            panelHeight,
            modelTolerance,
            "HORIZONTAL");
        OperationResponse<PanelCladdingAxisCorrespondence> vertical = AlignAxis(
            oldVerticalOffsets,
            newVerticalOffsets,
            panelWidth,
            modelTolerance,
            "VERTICAL");
        if (!horizontal.Success || horizontal.Data is null ||
            !vertical.Success || vertical.Data is null)
        {
            return OperationResponse<PanelCladdingLayoutReconciliationResult>.Fail(
                $"PANEL_CLADDING_RECONCILIATION_ALIGNMENT_FAILED: " +
                $"{horizontal.Message}{vertical.Message}");
        }

        PanelCladdingTopologyState topology = RemapTopology(
            oldTopology ?? new PanelCladdingTopologyState(),
            horizontal.Data,
            vertical.Data,
            oldHorizontalOffsets.Count,
            oldVerticalOffsets.Count,
            newHorizontalOffsets.Count,
            newVerticalOffsets.Count);
        return OperationResponse<PanelCladdingLayoutReconciliationResult>.Ok(
            new PanelCladdingLayoutReconciliationResult
            {
                Horizontal = horizontal.Data,
                Vertical = vertical.Data,
                Topology = topology
            });
    }

    public static OperationResponse<PanelCladdingAxisCorrespondence> AlignAxis(
        IReadOnlyList<double> oldOffsets,
        IReadOnlyList<double> newOffsets,
        double extent,
        double modelTolerance,
        string axisLabel)
    {
        oldOffsets ??= Array.Empty<double>();
        newOffsets ??= Array.Empty<double>();
        if (!ValidateOffsets(oldOffsets, extent, modelTolerance) ||
            !ValidateOffsets(newOffsets, extent, modelTolerance))
        {
            return OperationResponse<PanelCladdingAxisCorrespondence>.Fail(
                $"PANEL_CLADDING_RECONCILIATION_{axisLabel}_OFFSETS_INVALID");
        }

        int oldCount = oldOffsets.Count;
        int newCount = newOffsets.Count;
        var costs = new double[oldCount + 1, newCount + 1];
        var steps = new AlignmentStep[oldCount + 1, newCount + 1];
        for (int oldIndex = 0; oldIndex <= oldCount; oldIndex++)
        {
            for (int newIndex = 0; newIndex <= newCount; newIndex++)
            {
                costs[oldIndex, newIndex] = double.PositiveInfinity;
            }
        }
        costs[0, 0] = 0d;
        for (int oldIndex = 1; oldIndex <= oldCount; oldIndex++)
        {
            costs[oldIndex, 0] = costs[oldIndex - 1, 0] + InsertDeleteCost;
            steps[oldIndex, 0] = AlignmentStep.DeleteOld;
        }
        for (int newIndex = 1; newIndex <= newCount; newIndex++)
        {
            costs[0, newIndex] = costs[0, newIndex - 1] + InsertDeleteCost;
            steps[0, newIndex] = AlignmentStep.InsertNew;
        }

        double tolerance = Math.Max(modelTolerance * 10d, extent * 1e-7d);
        for (int oldIndex = 1; oldIndex <= oldCount; oldIndex++)
        {
            for (int newIndex = 1; newIndex <= newCount; newIndex++)
            {
                double distance = Math.Abs(
                    oldOffsets[oldIndex - 1] - newOffsets[newIndex - 1]);
                double matchPenalty = distance <= tolerance ? 0d : distance / extent;
                Choose(
                    costs,
                    steps,
                    oldIndex,
                    newIndex,
                    costs[oldIndex - 1, newIndex - 1] + matchPenalty,
                    AlignmentStep.Match,
                    priority: 0);
                Choose(
                    costs,
                    steps,
                    oldIndex,
                    newIndex,
                    costs[oldIndex, newIndex - 1] + InsertDeleteCost,
                    AlignmentStep.InsertNew,
                    priority: 1);
                Choose(
                    costs,
                    steps,
                    oldIndex,
                    newIndex,
                    costs[oldIndex - 1, newIndex] + InsertDeleteCost,
                    AlignmentStep.DeleteOld,
                    priority: 2);
            }
        }

        var oldToNew = new int?[oldCount];
        var newToOld = new int?[newCount];
        int oldCursor = oldCount;
        int newCursor = newCount;
        while (oldCursor > 0 || newCursor > 0)
        {
            AlignmentStep step = steps[oldCursor, newCursor];
            switch (step)
            {
                case AlignmentStep.Match:
                    oldToNew[oldCursor - 1] = newCursor - 1;
                    newToOld[newCursor - 1] = oldCursor - 1;
                    oldCursor--;
                    newCursor--;
                    break;
                case AlignmentStep.InsertNew:
                    newCursor--;
                    break;
                case AlignmentStep.DeleteOld:
                    oldCursor--;
                    break;
                default:
                    return OperationResponse<PanelCladdingAxisCorrespondence>.Fail(
                        $"PANEL_CLADDING_RECONCILIATION_{axisLabel}_ALIGNMENT_INCOMPLETE");
            }
        }

        int[] newBayToOldBay = BuildBayMap(oldOffsets, newOffsets, extent);
        return OperationResponse<PanelCladdingAxisCorrespondence>.Ok(
            new PanelCladdingAxisCorrespondence
            {
                OldToNewTracks = oldToNew,
                NewToOldTracks = newToOld,
                NewBayToOldBay = newBayToOldBay
            });
    }

    public static PanelCladdingTopologyState ApplyInsertedTrackEvidence(
        PanelCladdingLayoutReconciliationResult reconciliation,
        PanelCladdingTopologyState topology,
        IReadOnlyDictionary<string, string> surfaceOwnerByCellLabel,
        PanelCladdingTopologyState? curveTopology,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        var missing = topology.MissingSegments.ToHashSet();
        var hidden = topology.HiddenSegments.ToHashSet();
        var runs = topology.MergeRuns.ToList();
        PanelCladdingTopologyState curves = curveTopology ?? new PanelCladdingTopologyState();
        HashSet<PanelCladdingSegmentCoordinate> curveMissing = curves.MissingSegments.ToHashSet();

        ApplyAxis(
            PanelCladdingTopologyAxis.Horizontal,
            reconciliation.Horizontal.NewToOldTracks,
            horizontalTrackCount,
            verticalTrackCount + 1);
        ApplyAxis(
            PanelCladdingTopologyAxis.Vertical,
            reconciliation.Vertical.NewToOldTracks,
            verticalTrackCount,
            horizontalTrackCount + 1);

        return new PanelCladdingTopologyState
        {
            MissingSegments = missing
                .OrderBy(segment => segment.Axis)
                .ThenBy(segment => segment.Track)
                .ThenBy(segment => segment.Bay)
                .ToArray(),
            HiddenSegments = hidden
                .Where(segment => !missing.Contains(segment))
                .OrderBy(segment => segment.Axis)
                .ThenBy(segment => segment.Track)
                .ThenBy(segment => segment.Bay)
                .ToArray(),
            MergeRuns = runs
                .Where(run => Enumerable.Range(run.StartBay, run.EndBay - run.StartBay + 1)
                    .All(bay => !missing.Contains(new PanelCladdingSegmentCoordinate(
                        run.Axis,
                        run.Track,
                        bay))))
                .Distinct()
                .OrderBy(run => run.Axis)
                .ThenBy(run => run.Track)
                .ThenBy(run => run.StartBay)
                .ToArray()
        };

        void ApplyAxis(
            PanelCladdingTopologyAxis axis,
            IReadOnlyList<int?> newToOldTracks,
            int trackCount,
            int bayCount)
        {
            for (int track = 0; track < trackCount; track++)
            {
                if (track < newToOldTracks.Count && newToOldTracks[track].HasValue)
                {
                    continue;
                }
                for (int bay = 0; bay < bayCount; bay++)
                {
                    var coordinate = new PanelCladdingSegmentCoordinate(axis, track, bay);
                    (string First, string Second) labels = axis == PanelCladdingTopologyAxis.Horizontal
                        ? ($"{bay}{PanelCladdingKeyService.GetRowLabel(track)}",
                            $"{bay}{PanelCladdingKeyService.GetRowLabel(track + 1)}")
                        : ($"{track}{PanelCladdingKeyService.GetRowLabel(bay)}",
                            $"{track + 1}{PanelCladdingKeyService.GetRowLabel(bay)}");
                    string? firstOwner = null;
                    string? secondOwner = null;
                    bool surfaceEvidenceAvailable = surfaceOwnerByCellLabel.TryGetValue(
                            labels.First,
                            out firstOwner) &&
                        surfaceOwnerByCellLabel.TryGetValue(labels.Second, out secondOwner);
                    bool surfaceRequiresSegment = surfaceEvidenceAvailable && !string.Equals(
                        firstOwner,
                        secondOwner,
                        StringComparison.OrdinalIgnoreCase);
                    bool curveRequiresSegment = curveTopology is not null &&
                        !curveMissing.Contains(coordinate);
                    if (surfaceEvidenceAvailable && !surfaceRequiresSegment && !curveRequiresSegment)
                    {
                        missing.Add(coordinate);
                    }
                    else
                    {
                        missing.Remove(coordinate);
                    }
                }
                runs.RemoveAll(run => run.Axis == axis && run.Track == track);
                foreach (PanelCladdingMergeRun run in curves.MergeRuns.Where(run =>
                    run.Axis == axis && run.Track == track))
                {
                    runs.Add(run);
                }
            }
        }
    }

    private static PanelCladdingTopologyState RemapTopology(
        PanelCladdingTopologyState oldTopology,
        PanelCladdingAxisCorrespondence horizontal,
        PanelCladdingAxisCorrespondence vertical,
        int oldHorizontalTrackCount,
        int oldVerticalTrackCount,
        int newHorizontalTrackCount,
        int newVerticalTrackCount)
    {
        var missing = new HashSet<PanelCladdingSegmentCoordinate>();
        var hidden = new HashSet<PanelCladdingSegmentCoordinate>();
        var runs = new List<PanelCladdingMergeRun>();
        RemapAxis(
            PanelCladdingTopologyAxis.Horizontal,
            oldHorizontalTrackCount,
            newHorizontalTrackCount,
            oldVerticalTrackCount + 1,
            newVerticalTrackCount + 1,
            horizontal.NewToOldTracks,
            vertical.NewBayToOldBay,
            oldTopology,
            missing,
            hidden,
            runs);
        RemapAxis(
            PanelCladdingTopologyAxis.Vertical,
            oldVerticalTrackCount,
            newVerticalTrackCount,
            oldHorizontalTrackCount + 1,
            newHorizontalTrackCount + 1,
            vertical.NewToOldTracks,
            horizontal.NewBayToOldBay,
            oldTopology,
            missing,
            hidden,
            runs);
        return new PanelCladdingTopologyState
        {
            MissingSegments = missing
                .OrderBy(segment => segment.Axis)
                .ThenBy(segment => segment.Track)
                .ThenBy(segment => segment.Bay)
                .ToArray(),
            HiddenSegments = hidden
                .OrderBy(segment => segment.Axis)
                .ThenBy(segment => segment.Track)
                .ThenBy(segment => segment.Bay)
                .ToArray(),
            MergeRuns = runs
                .OrderBy(run => run.Axis)
                .ThenBy(run => run.Track)
                .ThenBy(run => run.StartBay)
                .ToArray()
        };
    }

    private static void RemapAxis(
        PanelCladdingTopologyAxis axis,
        int oldTrackCount,
        int newTrackCount,
        int oldBayCount,
        int newBayCount,
        IReadOnlyList<int?> newToOldTracks,
        IReadOnlyList<int> newBayToOldBay,
        PanelCladdingTopologyState oldTopology,
        ISet<PanelCladdingSegmentCoordinate> missing,
        ISet<PanelCladdingSegmentCoordinate> hidden,
        ICollection<PanelCladdingMergeRun> runs)
    {
        HashSet<PanelCladdingSegmentCoordinate> oldMissing = oldTopology.MissingSegments
            .Where(segment => segment.Axis == axis)
            .ToHashSet();
        HashSet<PanelCladdingSegmentCoordinate> oldHidden = oldTopology.HiddenSegments
            .Where(segment => segment.Axis == axis)
            .ToHashSet();
        for (int newTrack = 0; newTrack < newTrackCount; newTrack++)
        {
            int? oldTrack = newTrack < newToOldTracks.Count
                ? newToOldTracks[newTrack]
                : null;
            if (!oldTrack.HasValue || oldTrack.Value < 0 || oldTrack.Value >= oldTrackCount)
            {
                continue;
            }

            int[] groupParents = Enumerable.Range(0, oldBayCount).ToArray();
            foreach (PanelCladdingMergeRun run in oldTopology.MergeRuns.Where(run =>
                run.Axis == axis && run.Track == oldTrack.Value))
            {
                for (int bay = run.StartBay + 1; bay <= run.EndBay; bay++)
                {
                    Union(groupParents, run.StartBay, bay);
                }
            }

            var groupByNewBay = new int?[newBayCount];
            for (int newBay = 0; newBay < newBayCount; newBay++)
            {
                int oldBay = newBay < newBayToOldBay.Count
                    ? newBayToOldBay[newBay]
                    : Math.Min(newBay, oldBayCount - 1);
                oldBay = Math.Clamp(oldBay, 0, oldBayCount - 1);
                var oldCoordinate = new PanelCladdingSegmentCoordinate(
                    axis,
                    oldTrack.Value,
                    oldBay);
                var newCoordinate = new PanelCladdingSegmentCoordinate(axis, newTrack, newBay);
                if (oldMissing.Contains(oldCoordinate))
                {
                    missing.Add(newCoordinate);
                    continue;
                }
                if (oldHidden.Contains(oldCoordinate))
                {
                    hidden.Add(newCoordinate);
                }
                groupByNewBay[newBay] = Find(groupParents, oldBay);
            }

            int start = 0;
            while (start < newBayCount)
            {
                if (!groupByNewBay[start].HasValue)
                {
                    start++;
                    continue;
                }
                int end = start;
                while (end + 1 < newBayCount &&
                    groupByNewBay[end + 1] == groupByNewBay[start])
                {
                    end++;
                }
                if (end > start)
                {
                    runs.Add(new PanelCladdingMergeRun(axis, newTrack, start, end));
                }
                start = end + 1;
            }
        }
    }

    private static int[] BuildBayMap(
        IReadOnlyList<double> oldOffsets,
        IReadOnlyList<double> newOffsets,
        double extent)
    {
        double[] oldBoundaries = new[] { 0d }
            .Concat(oldOffsets)
            .Append(extent)
            .ToArray();
        double[] newBoundaries = new[] { 0d }
            .Concat(newOffsets)
            .Append(extent)
            .ToArray();
        var result = new int[newBoundaries.Length - 1];
        for (int bay = 0; bay < result.Length; bay++)
        {
            double midpoint = (newBoundaries[bay] + newBoundaries[bay + 1]) * 0.5d;
            int oldBay = Array.FindIndex(oldBoundaries, 1, boundary => midpoint < boundary);
            result[bay] = oldBay < 0 ? oldBoundaries.Length - 2 : oldBay - 1;
        }
        return result;
    }

    private static bool ValidateOffsets(
        IReadOnlyList<double> offsets,
        double extent,
        double tolerance)
    {
        if (!double.IsFinite(extent) || extent <= 0d ||
            !double.IsFinite(tolerance) || tolerance <= 0d)
        {
            return false;
        }
        double previous = 0d;
        foreach (double value in offsets)
        {
            if (!double.IsFinite(value) || value <= previous + tolerance ||
                value >= extent - tolerance)
            {
                return false;
            }
            previous = value;
        }
        return true;
    }

    private static void Choose(
        double[,] costs,
        AlignmentStep[,] steps,
        int oldIndex,
        int newIndex,
        double candidateCost,
        AlignmentStep candidateStep,
        int priority)
    {
        double current = costs[oldIndex, newIndex];
        int currentPriority = StepPriority(steps[oldIndex, newIndex]);
        if (candidateCost < current - CostEpsilon ||
            (Math.Abs(candidateCost - current) <= CostEpsilon && priority < currentPriority))
        {
            costs[oldIndex, newIndex] = candidateCost;
            steps[oldIndex, newIndex] = candidateStep;
        }
    }

    private static int StepPriority(AlignmentStep step) => step switch
    {
        AlignmentStep.Match => 0,
        AlignmentStep.InsertNew => 1,
        AlignmentStep.DeleteOld => 2,
        _ => int.MaxValue
    };

    private static int Find(int[] parents, int index)
    {
        int root = index;
        while (parents[root] != root)
        {
            root = parents[root];
        }
        while (parents[index] != index)
        {
            int next = parents[index];
            parents[index] = root;
            index = next;
        }
        return root;
    }

    private static void Union(int[] parents, int first, int second)
    {
        int firstRoot = Find(parents, first);
        int secondRoot = Find(parents, second);
        if (firstRoot != secondRoot)
        {
            parents[secondRoot] = firstRoot;
        }
    }

    private enum AlignmentStep
    {
        None,
        Match,
        InsertNew,
        DeleteOld
    }
}
