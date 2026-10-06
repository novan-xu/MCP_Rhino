using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingCreatePlanningService
{
    private const double OrthogonalRatio = 2d;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingLogicService _claddingLogic;
    private readonly PanelCladdingLogicalCellService _logicalCells;

    public PanelCladdingCreatePlanningService(PanelCladdingKeyService keys)
    {
        _keys = keys;
        _claddingLogic = new PanelCladdingLogicService();
        _logicalCells = new PanelCladdingLogicalCellService();
    }

    public OperationResponse<PanelCladdingCreatePlan> CreatePlan(
        IReadOnlyList<PanelCladdingCreatePanelSnapshot> snapshots)
    {
        PanelCladdingCreatePanelSnapshot[] panels = (snapshots ?? Array.Empty<PanelCladdingCreatePanelSnapshot>())
            .Where(snapshot => snapshot.ObjectId != Guid.Empty)
            .GroupBy(snapshot => snapshot.ObjectId)
            .Select(group => group.First())
            .ToArray();
        if (panels.Length == 0)
        {
            return OperationResponse<PanelCladdingCreatePlan>.Fail(
                "PANEL_CLADDING_CREATE_PANEL_SELECTION_REQUIRED");
        }

        var plans = new List<PanelCladdingCreatePanelPlan>(panels.Length);
        foreach (PanelCladdingCreatePanelSnapshot panel in panels)
        {
            OperationResponse<PanelCladdingCreatePanelPlan> planned = CreatePanelPlan(panel);
            if (!planned.Success || planned.Data is null)
            {
                return OperationResponse<PanelCladdingCreatePlan>.Fail(planned.Message);
            }
            plans.Add(planned.Data);
        }
        return OperationResponse<PanelCladdingCreatePlan>.Ok(new PanelCladdingCreatePlan
        {
            Panels = plans
        });
    }

    private OperationResponse<PanelCladdingCreatePanelPlan> CreatePanelPlan(
        PanelCladdingCreatePanelSnapshot panel)
    {
        double width = panel.XMaximum - panel.XMinimum;
        double height = panel.YMaximum - panel.YMinimum;
        double tolerance = Math.Max(panel.Tolerance, 1e-9d);
        if (!double.IsFinite(width) || !double.IsFinite(height) ||
            width <= tolerance || height <= tolerance)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_PANEL_EXTENT_INVALID: {panel.ObjectId:D}");
        }
        if (panel.Guides.Count == 0)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_GUIDE_SELECTION_REQUIRED: {panel.ObjectId:D}");
        }

        double maximumExtent = Math.Max(width, height);
        double axisTolerance = Math.Max(tolerance * 10d, maximumExtent * 1e-7d);
        double duplicateTolerance = Math.Max(tolerance * 2d, maximumExtent * 1e-8d);
        double depthAllowance = Math.Max(tolerance * 10d, maximumExtent * 0.01d);
        var candidates = new List<GuideCandidate>();
        var warnings = new List<string>();

        foreach (PanelCladdingCreateGuideSnapshot guide in panel.Guides)
        {
            if (!guide.IsOnPanel)
            {
                warnings.Add(
                    $"PANEL_CLADDING_CREATE_GUIDE_NOT_ON_PANEL: panel {panel.ObjectId:D}, guide {guide.ObjectId:D}");
                continue;
            }

            PanelPoint3[] samples = guide.Samples
                .Where(sample => double.IsFinite(sample.X) && double.IsFinite(sample.Y) && double.IsFinite(sample.Z))
                .ToArray();
            if (samples.Length < 2)
            {
                warnings.Add(
                    $"PANEL_CLADDING_CREATE_GUIDE_SAMPLES_REQUIRED: panel {panel.ObjectId:D}, guide {guide.ObjectId:D}");
                continue;
            }

            double zMinimum = samples.Min(sample => sample.Z);
            double zMaximum = samples.Max(sample => sample.Z);
            if (zMaximum < panel.ZMinimum - depthAllowance || zMinimum > panel.ZMaximum + depthAllowance)
            {
                continue;
            }

            double xMinimum = samples.Min(sample => sample.X);
            double xMaximum = samples.Max(sample => sample.X);
            double yMinimum = samples.Min(sample => sample.Y);
            double yMaximum = samples.Max(sample => sample.Y);
            double deltaX = xMaximum - xMinimum;
            double deltaY = yMaximum - yMinimum;

            if (deltaY >= Math.Max(deltaX * OrthogonalRatio, axisTolerance))
            {
                double position = Median(samples.Select(sample => sample.X));
                double start = Math.Max(yMinimum, panel.YMinimum) - panel.YMinimum;
                double end = Math.Min(yMaximum, panel.YMaximum) - panel.YMinimum;
                if (position > panel.XMinimum + axisTolerance &&
                    position < panel.XMaximum - axisTolerance && end - start > axisTolerance)
                {
                    candidates.Add(new GuideCandidate(
                        guide.ObjectId,
                        PanelCladdingTopologyAxis.Vertical,
                        position - panel.XMinimum,
                        Math.Max(0d, start),
                        Math.Min(height, end)));
                }
            }
            else if (deltaX >= Math.Max(deltaY * OrthogonalRatio, axisTolerance))
            {
                double position = Median(samples.Select(sample => sample.Y));
                double start = Math.Max(xMinimum, panel.XMinimum) - panel.XMinimum;
                double end = Math.Min(xMaximum, panel.XMaximum) - panel.XMinimum;
                if (position > panel.YMinimum + axisTolerance &&
                    position < panel.YMaximum - axisTolerance && end - start > axisTolerance)
                {
                    candidates.Add(new GuideCandidate(
                        guide.ObjectId,
                        PanelCladdingTopologyAxis.Horizontal,
                        position - panel.YMinimum,
                        Math.Max(0d, start),
                        Math.Min(width, end)));
                }
            }
            else
            {
                warnings.Add(
                    $"PANEL_CLADDING_CREATE_GUIDE_DIAGONAL: panel {panel.ObjectId:D}, guide {guide.ObjectId:D}");
            }
        }

        double[] horizontal = ClusterOffsets(
            candidates.Where(candidate => candidate.Axis == PanelCladdingTopologyAxis.Horizontal)
                .Select(candidate => candidate.Position)
                .ToArray(),
            duplicateTolerance).ToArray();
        double[] vertical = ClusterOffsets(
            candidates.Where(candidate => candidate.Axis == PanelCladdingTopologyAxis.Vertical)
                .Select(candidate => candidate.Position)
                .ToArray(),
            duplicateTolerance).ToArray();

        TopologyInference inferred = InferClosedTopology(
            candidates,
            horizontal,
            vertical,
            width,
            height,
            axisTolerance);
        horizontal = inferred.HorizontalOffsets.ToArray();
        vertical = inferred.VerticalOffsets.ToArray();
        if (horizontal.Length + vertical.Length == 0)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_APPLICABLE_GUIDES_REQUIRED: {panel.ObjectId:D}");
        }

        OperationResponse<PanelCladdingKeySet> keySet = _keys.CreateKeySet(
            horizontal,
            vertical,
            new Dictionary<string, string>(),
            width,
            height,
            tolerance);
        if (!keySet.Success || keySet.Data is null)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_GRID_INVALID: {panel.ObjectId:D}: {keySet.Message}");
        }
        OperationResponse<IReadOnlyDictionary<string, string>> masks = _keys.EncodeNonDefaultTopology(
            inferred.Topology,
            horizontal.Length,
            vertical.Length);
        if (!masks.Success || masks.Data is null)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_TOPOLOGY_INVALID: {panel.ObjectId:D}: {masks.Message}");
        }

        string[] deletes = panel.UserText.Keys
            .Where(key => ResetAttributeRegex().IsMatch(key) ||
                PanelCladdingKeyService.IsTopologyKey(key) ||
                string.Equals(key, PanelCladdingKeyService.UnitDimensionKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.UnitWidthKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.UnitHeightKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.SignatureKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string unitWidth = PanelCladdingKeyService.FormatUnitDimension(width);
        string unitHeight = PanelCladdingKeyService.FormatUnitDimension(height);
        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.UnitDimensionKey] = $"{unitWidth}x{unitHeight}",
            [PanelCladdingKeyService.UnitWidthKey] = unitWidth,
            [PanelCladdingKeyService.UnitHeightKey] = unitHeight
        };
        PanelCladdingCidService.AddPanelCidWrite(writes, panel.UserText);
        for (int index = 0; index < horizontal.Length; index++)
        {
            writes[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(horizontal[index]);
        }
        for (int index = 0; index < vertical.Length; index++)
        {
            writes[PanelCladdingKeyService.GetVerticalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(vertical[index]);
        }
        foreach ((string key, string value) in masks.Data)
        {
            writes[key] = value;
        }

        var blanks = keySet.Data.Cells.ToDictionary(
            cell => cell.UserTextKey,
            _ => string.Empty,
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> logicalBlanks = _logicalCells.Collapse(
            keySet.Data.Cells,
            inferred.Topology,
            blanks);
        foreach ((string key, string value) in logicalBlanks)
        {
            writes[key] = PanelCladdingKeyService.EncodeCellValueForStorage(value);
        }
        OperationResponse<string> claddingLogic = _claddingLogic.Encode(
            keySet.Data.Cells,
            blanks);
        if (!claddingLogic.Success || claddingLogic.Data is null)
        {
            return OperationResponse<PanelCladdingCreatePanelPlan>.Fail(
                $"PANEL_CLADDING_CREATE_LOGIC_INVALID: {panel.ObjectId:D}: {claddingLogic.Message}");
        }
        writes[PanelCladdingKeyService.CladdingLogicKey] = claddingLogic.Data;

        return OperationResponse<PanelCladdingCreatePanelPlan>.Ok(new PanelCladdingCreatePanelPlan
        {
            ObjectId = panel.ObjectId,
            HorizontalOffsets = horizontal,
            VerticalOffsets = vertical,
            UserTextDeletes = deletes,
            UserTextWrites = writes,
            Warnings = warnings
        });
    }

    private static TopologyInference InferClosedTopology(
        IReadOnlyList<GuideCandidate> candidates,
        IReadOnlyList<double> initialHorizontal,
        IReadOnlyList<double> initialVertical,
        double width,
        double height,
        double tolerance)
    {
        double[] horizontal = initialHorizontal.ToArray();
        double[] vertical = initialVertical.ToArray();
        PanelCladdingTopologyState topology = new();
        int iterationLimit = horizontal.Length + vertical.Length + 1;
        for (int iteration = 0; iteration < iterationLimit; iteration++)
        {
            TrackTopology built = BuildTrackTopology(
                candidates,
                horizontal,
                vertical,
                width,
                height,
                tolerance);
            double[] retainedHorizontal = horizontal
                .Where((_, track) => HasTrack(
                    built.Present,
                    PanelCladdingTopologyAxis.Horizontal,
                    track))
                .ToArray();
            double[] retainedVertical = vertical
                .Where((_, track) => HasTrack(
                    built.Present,
                    PanelCladdingTopologyAxis.Vertical,
                    track))
                .ToArray();
            topology = built.Topology;
            if (retainedHorizontal.Length == horizontal.Length &&
                retainedVertical.Length == vertical.Length)
            {
                return new TopologyInference(horizontal, vertical, topology);
            }
            horizontal = retainedHorizontal;
            vertical = retainedVertical;
        }
        return new TopologyInference(horizontal, vertical, topology);
    }

    private static bool HasTrack(
        IReadOnlySet<PanelCladdingSegmentCoordinate> segments,
        PanelCladdingTopologyAxis axis,
        int track) => segments.Any(segment => segment.Axis == axis && segment.Track == track);

    private static TrackTopology BuildTrackTopology(
        IReadOnlyList<GuideCandidate> candidates,
        IReadOnlyList<double> horizontal,
        IReadOnlyList<double> vertical,
        double width,
        double height,
        double tolerance)
    {
        double[] xCuts = [0d, .. vertical, width];
        double[] yCuts = [0d, .. horizontal, height];
        var present = new HashSet<PanelCladdingSegmentCoordinate>();

        BuildAxis(
            PanelCladdingTopologyAxis.Horizontal,
            horizontal,
            xCuts,
            candidates,
            tolerance,
            present);
        BuildAxis(
            PanelCladdingTopologyAxis.Vertical,
            vertical,
            yCuts,
            candidates,
            tolerance,
            present);

        var missing = new List<PanelCladdingSegmentCoordinate>();
        for (int track = 0; track < horizontal.Count; track++)
        {
            for (int bay = 0; bay < xCuts.Length - 1; bay++)
            {
                var segment = new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Horizontal,
                    track,
                    bay);
                if (!present.Contains(segment)) missing.Add(segment);
            }
        }
        for (int track = 0; track < vertical.Count; track++)
        {
            for (int bay = 0; bay < yCuts.Length - 1; bay++)
            {
                var segment = new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Vertical,
                    track,
                    bay);
                if (!present.Contains(segment)) missing.Add(segment);
            }
        }
        return new TrackTopology(present, new PanelCladdingTopologyState
        {
            MissingSegments = missing
        });
    }

    private static void BuildAxis(
        PanelCladdingTopologyAxis axis,
        IReadOnlyList<double> offsets,
        IReadOnlyList<double> cuts,
        IReadOnlyList<GuideCandidate> candidates,
        double tolerance,
        ISet<PanelCladdingSegmentCoordinate> present)
    {
        for (int track = 0; track < offsets.Count; track++)
        {
            GuideCandidate[] guides = candidates
                .Where(candidate => candidate.Axis == axis &&
                    Math.Abs(candidate.Position - offsets[track]) <= tolerance)
                .ToArray();
            // Guides establish coverage only; new atoms remain segmented until explicitly merged.
            for (int bay = 0; bay < cuts.Count - 1; bay++)
            {
                if (IsIntervalCovered(guides, cuts[bay], cuts[bay + 1], tolerance))
                {
                    present.Add(new PanelCladdingSegmentCoordinate(axis, track, bay));
                }
            }
        }
    }

    private static bool IsIntervalCovered(
        IReadOnlyList<GuideCandidate> guides,
        double start,
        double end,
        double tolerance)
    {
        GuideCandidate[] ordered = guides
            .Where(guide => guide.End >= start - tolerance && guide.Start <= end + tolerance)
            .OrderBy(guide => guide.Start)
            .ThenBy(guide => guide.End)
            .ToArray();
        if (ordered.Length == 0 || ordered[0].Start > start + tolerance)
        {
            return false;
        }
        double covered = ordered[0].End;
        if (covered >= end - tolerance)
        {
            return true;
        }
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Start > covered + tolerance)
            {
                return false;
            }
            covered = Math.Max(covered, ordered[index].End);
            if (covered >= end - tolerance)
            {
                return true;
            }
        }
        return false;
    }

    private static IReadOnlyList<double> ClusterOffsets(
        IReadOnlyList<double> candidates,
        double tolerance)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<double>();
        }
        double[] ordered = candidates.OrderBy(value => value).ToArray();
        var clusters = new List<List<double>> { new() { ordered[0] } };
        for (int index = 1; index < ordered.Length; index++)
        {
            List<double> current = clusters[^1];
            if (ordered[index] <= current.Average() + tolerance)
            {
                current.Add(ordered[index]);
            }
            else
            {
                clusters.Add(new List<double> { ordered[index] });
            }
        }
        return clusters
            .Select(cluster => PanelCladdingKeyService.NormalizeOffset(cluster.Average()))
            .ToArray();
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2d;
    }

    private sealed record GuideCandidate(
        Guid ObjectId,
        PanelCladdingTopologyAxis Axis,
        double Position,
        double Start,
        double End);

    private sealed record TopologyInference(
        IReadOnlyList<double> HorizontalOffsets,
        IReadOnlyList<double> VerticalOffsets,
        PanelCladdingTopologyState Topology);

    private sealed record TrackTopology(
        IReadOnlySet<PanelCladdingSegmentCoordinate> Present,
        PanelCladdingTopologyState Topology);

    [GeneratedRegex(
        @"^CW_\d+\.\d{2}_(?:OFFSET|CLADDING)_.+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResetAttributeRegex();
}
