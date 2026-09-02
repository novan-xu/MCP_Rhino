using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public readonly record struct PanelCladdingCollapsedTrack(
    PanelCladdingTopologyAxis Axis,
    int Track);

public sealed class PanelCladdingTopologyNormalizationResult
{
    public IReadOnlyList<double> HorizontalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyList<double> VerticalOffsets { get; init; } = Array.Empty<double>();
    public IReadOnlyDictionary<string, string> CellValues { get; init; } =
        new Dictionary<string, string>();
    public PanelCladdingTopologyState Topology { get; init; } = new();
    public IReadOnlyList<PanelCladdingCollapsedTrack> CollapsedTracks { get; init; } =
        Array.Empty<PanelCladdingCollapsedTrack>();
}

public sealed class PanelCladdingTopologyNormalizationService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingTopologyNormalizationService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingTopologyNormalizationResult> Normalize(
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        IReadOnlyDictionary<string, string> cellValues,
        PanelCladdingTopologyState topology,
        double panelWidth,
        double panelHeight,
        double tolerance)
    {
        var horizontal = horizontalOffsets.ToList();
        var vertical = verticalOffsets.ToList();
        var values = new Dictionary<string, string>(cellValues, StringComparer.OrdinalIgnoreCase);
        PanelCladdingTopologyState currentTopology = topology;
        var collapsed = new List<PanelCladdingCollapsedTrack>();

        while (TryFindCompleteTrack(
            currentTopology,
            horizontal.Count,
            vertical.Count,
            out PanelCladdingCollapsedTrack complete))
        {
            OperationResponse<PanelCladdingKeySet> keySet = _keys.CreateKeySet(
                horizontal,
                vertical,
                values,
                panelWidth,
                panelHeight,
                tolerance);
            if (!keySet.Success || keySet.Data is null)
            {
                return OperationResponse<PanelCladdingTopologyNormalizationResult>.Fail(keySet.Message);
            }

            values = CollapseValues(keySet.Data.Cells, values, complete);
            currentTopology = CollapseTopology(currentTopology, complete);
            if (complete.Axis == PanelCladdingTopologyAxis.Horizontal)
            {
                horizontal.RemoveAt(complete.Track);
            }
            else
            {
                vertical.RemoveAt(complete.Track);
            }
            collapsed.Add(complete);
        }

        OperationResponse<PanelCladdingTopologyPayloads> topologyValidation = _keys.EncodeTopology(
            currentTopology,
            horizontal.Count,
            vertical.Count);
        if (!topologyValidation.Success)
        {
            return OperationResponse<PanelCladdingTopologyNormalizationResult>.Fail(topologyValidation.Message);
        }

        return OperationResponse<PanelCladdingTopologyNormalizationResult>.Ok(
            new PanelCladdingTopologyNormalizationResult
            {
                HorizontalOffsets = horizontal,
                VerticalOffsets = vertical,
                CellValues = values,
                Topology = currentTopology,
                CollapsedTracks = collapsed
            });
    }

    private static bool TryFindCompleteTrack(
        PanelCladdingTopologyState topology,
        int horizontalTrackCount,
        int verticalTrackCount,
        out PanelCladdingCollapsedTrack complete)
    {
        HashSet<PanelCladdingSegmentCoordinate> missing = topology.MissingSegments.ToHashSet();
        for (int track = 0; track < horizontalTrackCount; track++)
        {
            if (Enumerable.Range(0, verticalTrackCount + 1).All(bay =>
                missing.Contains(new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Horizontal,
                    track,
                    bay))))
            {
                complete = new PanelCladdingCollapsedTrack(PanelCladdingTopologyAxis.Horizontal, track);
                return true;
            }
        }
        for (int track = 0; track < verticalTrackCount; track++)
        {
            if (Enumerable.Range(0, horizontalTrackCount + 1).All(bay =>
                missing.Contains(new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Vertical,
                    track,
                    bay))))
            {
                complete = new PanelCladdingCollapsedTrack(PanelCladdingTopologyAxis.Vertical, track);
                return true;
            }
        }
        complete = default;
        return false;
    }

    private static Dictionary<string, string> CollapseValues(
        IReadOnlyList<PanelCladdingCell> cells,
        IReadOnlyDictionary<string, string> values,
        PanelCladdingCollapsedTrack collapsed)
    {
        var labelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in cells)
        {
            (int column, int row) = MapCell(cell.Column, cell.Row, collapsed);
            labelMap[cell.ShortLabel] = $"{column}{PanelCladdingKeyService.GetRowLabel(row)}";
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in cells
                     .OrderBy(item => item.Row)
                     .ThenBy(item => item.Column))
        {
            (int column, int row) = MapCell(cell.Column, cell.Row, collapsed);
            string key = PanelCladdingKeyService.GetCellKey(
                column,
                PanelCladdingKeyService.GetRowLabel(row));
            if (!result.ContainsKey(key))
            {
                result[key] = values.GetValueOrDefault(cell.UserTextKey, cell.Value);
            }
        }

        foreach (string key in result.Keys.ToArray())
        {
            string value = result[key].Trim();
            if (PanelCladdingKeyService.IsCellLabelToken(value) &&
                labelMap.TryGetValue(value, out string? remapped))
            {
                result[key] = remapped;
            }
        }
        return result;
    }

    private static (int Column, int Row) MapCell(
        int column,
        int row,
        PanelCladdingCollapsedTrack collapsed)
    {
        if (collapsed.Axis == PanelCladdingTopologyAxis.Horizontal)
        {
            return (column, row <= collapsed.Track ? row : row - 1);
        }
        return (column <= collapsed.Track ? column : column - 1, row);
    }

    private static PanelCladdingTopologyState CollapseTopology(
        PanelCladdingTopologyState topology,
        PanelCladdingCollapsedTrack collapsed)
    {
        PanelCladdingSegmentCoordinate[] missing = topology.MissingSegments
            .Select(segment => MapSegment(segment, collapsed))
            .Where(segment => segment.HasValue)
            .Select(segment => segment!.Value)
            .Distinct()
            .OrderBy(segment => segment.Axis)
            .ThenBy(segment => segment.Track)
            .ThenBy(segment => segment.Bay)
            .ToArray();
        HashSet<PanelCladdingSegmentCoordinate> missingSet = missing.ToHashSet();
        PanelCladdingSegmentCoordinate[] hidden = topology.HiddenSegments
            .Select(segment => MapSegment(segment, collapsed))
            .Where(segment => segment.HasValue)
            .Select(segment => segment!.Value)
            .Where(segment => !missingSet.Contains(segment))
            .Distinct()
            .OrderBy(segment => segment.Axis)
            .ThenBy(segment => segment.Track)
            .ThenBy(segment => segment.Bay)
            .ToArray();
        var runs = new List<PanelCladdingMergeRun>();
        foreach (PanelCladdingMergeRun run in topology.MergeRuns)
        {
            PanelCladdingSegmentCoordinate[] mapped = Enumerable
                .Range(run.StartBay, run.EndBay - run.StartBay + 1)
                .Select(bay => MapSegment(
                    new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay),
                    collapsed))
                .Where(segment => segment.HasValue)
                .Select(segment => segment!.Value)
                .Where(segment => !missingSet.Contains(segment))
                .Distinct()
                .OrderBy(segment => segment.Bay)
                .ToArray();
            foreach (IGrouping<(PanelCladdingTopologyAxis Axis, int Track), PanelCladdingSegmentCoordinate> group in
                     mapped.GroupBy(segment => (segment.Axis, segment.Track)))
            {
                AddRuns(group.Key.Axis, group.Key.Track, group.Select(segment => segment.Bay), runs);
            }
        }
        return new PanelCladdingTopologyState
        {
            MissingSegments = missing,
            HiddenSegments = hidden,
            MergeRuns = runs
                .OrderBy(run => run.Axis)
                .ThenBy(run => run.Track)
                .ThenBy(run => run.StartBay)
                .ToArray()
        };
    }

    private static PanelCladdingSegmentCoordinate? MapSegment(
        PanelCladdingSegmentCoordinate segment,
        PanelCladdingCollapsedTrack collapsed)
    {
        if (segment.Axis == collapsed.Axis)
        {
            if (segment.Track == collapsed.Track)
            {
                return null;
            }
            return segment with
            {
                Track = segment.Track < collapsed.Track ? segment.Track : segment.Track - 1
            };
        }
        return segment with
        {
            Bay = segment.Bay <= collapsed.Track ? segment.Bay : segment.Bay - 1
        };
    }

    private static void AddRuns(
        PanelCladdingTopologyAxis axis,
        int track,
        IEnumerable<int> bays,
        ICollection<PanelCladdingMergeRun> runs)
    {
        int[] ordered = bays.Distinct().OrderBy(bay => bay).ToArray();
        if (ordered.Length < 2)
        {
            return;
        }
        int start = ordered[0];
        int previous = ordered[0];
        foreach (int bay in ordered.Skip(1))
        {
            if (bay != previous + 1)
            {
                if (previous > start)
                {
                    runs.Add(new PanelCladdingMergeRun(axis, track, start, previous));
                }
                start = bay;
            }
            previous = bay;
        }
        if (previous > start)
        {
            runs.Add(new PanelCladdingMergeRun(axis, track, start, previous));
        }
    }
}
