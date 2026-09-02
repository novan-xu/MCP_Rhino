using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingMatchFeasibilityService
{
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingRegionService _regions;
    private readonly PanelCladdingLogicService _logic;
    private readonly PanelCladdingLogicalCellService _logicalCells;

    public PanelCladdingMatchFeasibilityService(PanelCladdingKeyService keys)
    {
        _keys = keys;
        _regions = new PanelCladdingRegionService(keys);
        _logic = new PanelCladdingLogicService();
        _logicalCells = new PanelCladdingLogicalCellService();
    }

    public OperationResponse<PanelCladdingMatchMapping> CreateMapping(
        PanelCladdingKeySet source,
        PanelCladdingKeySet target)
    {
        OperationResponse<SourcePartition> partitionResponse = BuildSourcePartition(source);
        if (!partitionResponse.Success || partitionResponse.Data is null)
        {
            return OperationResponse<PanelCladdingMatchMapping>.Fail(partitionResponse.Message);
        }
        SourcePartition partition = partitionResponse.Data;
        int targetColumnCount = target.VerticalOffsets.Count + 1;
        int targetRowCount = target.HorizontalOffsets.Count + 1;
        if (targetColumnCount < partition.ColumnBandCount ||
            targetRowCount < partition.RowBandCount)
        {
            return OperationResponse<PanelCladdingMatchMapping>.Fail(
                $"PANEL_CLADDING_MATCH_TARGET_BANDS_INSUFFICIENT: " +
                $"source={partition.ColumnBandCount}x{partition.RowBandCount}, " +
                $"target={targetColumnCount}x{targetRowCount}");
        }

        int[]? selectedColumns = null;
        int[]? selectedRows = null;
        foreach (int[] columnMap in EnumerateCandidateMaps(
            targetColumnCount,
            partition.ColumnBandCount,
            partition.SourceColumnBands))
        {
            foreach (int[] rowMap in EnumerateCandidateMaps(
                targetRowCount,
                partition.RowBandCount,
                partition.SourceRowBands))
            {
                if (TargetMissingSegmentsAreRepresentable(
                    target.Topology,
                    partition,
                    columnMap,
                    rowMap,
                    targetColumnCount,
                    targetRowCount))
                {
                    selectedColumns = columnMap;
                    selectedRows = rowMap;
                    break;
                }
            }
            if (selectedColumns is not null)
            {
                break;
            }
        }
        if (selectedColumns is null || selectedRows is null)
        {
            return OperationResponse<PanelCladdingMatchMapping>.Fail(
                "PANEL_CLADDING_MATCH_TARGET_PARTITION_NOT_REPRESENTABLE");
        }

        PanelCladdingCell[] targetCells = target.Cells
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();
        var assigned = targetCells.ToDictionary(
            cell => cell.UserTextKey,
            cell => partition.OwnerByBand[
                selectedColumns[cell.Column],
                selectedRows[cell.Row]],
            StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<int, PanelCladdingCell> group in targetCells.GroupBy(cell =>
            assigned[cell.UserTextKey]))
        {
            string material = partition.MaterialByOwner[group.Key];
            PanelCladdingCell owner = group
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .First();
            foreach (PanelCladdingCell cell in group)
            {
                values[cell.UserTextKey] = material.Length == 0 ||
                    string.Equals(
                        cell.UserTextKey,
                        owner.UserTextKey,
                        StringComparison.OrdinalIgnoreCase)
                    ? material
                    : owner.ShortLabel;
            }
        }

        OperationResponse<string> logic = _logic.Encode(target.Cells, values);
        return !logic.Success || logic.Data is null
            ? OperationResponse<PanelCladdingMatchMapping>.Fail(logic.Message)
            : OperationResponse<PanelCladdingMatchMapping>.Ok(
                new PanelCladdingMatchMapping
                {
                    TargetCellValues = values,
                    CladdingLogic = logic.Data
                });
    }

    private OperationResponse<SourcePartition> BuildSourcePartition(
        PanelCladdingKeySet source)
    {
        IReadOnlyDictionary<string, string> physicalValues = source.Cells.ToDictionary(
            cell => cell.UserTextKey,
            cell => _keys.NormalizeCladdingValue(cell.Value),
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> logicalValues = _logicalCells.Collapse(
            source.Cells,
            source.Topology,
            physicalValues);
        IReadOnlyDictionary<string, string> effectiveValues = _logicalCells.Expand(
            source.Cells,
            source.Topology,
            logicalValues);
        OperationResponse<PanelCladdingRegionSet> regions = _regions.Resolve(
            source.Cells,
            effectiveValues,
            requirePopulatedCells: false);
        if (!regions.Success || regions.Data is null)
        {
            return OperationResponse<SourcePartition>.Fail(regions.Message);
        }

        int columnCount = source.VerticalOffsets.Count + 1;
        int rowCount = source.HorizontalOffsets.Count + 1;
        PanelCladdingCell[,] cells = new PanelCladdingCell[columnCount, rowCount];
        foreach (PanelCladdingCell cell in source.Cells)
        {
            cells[cell.Column, cell.Row] = cell;
        }
        var ownerTokenByCoordinate = new string[columnCount, rowCount];
        var materialByToken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in source.Cells)
        {
            string value = regions.Data.NormalizedCellValues.GetValueOrDefault(
                cell.UserTextKey,
                string.Empty);
            if (value.Length == 0)
            {
                continue;
            }
            string ownerToken = PanelCladdingKeyService.IsCellLabelToken(value)
                ? value.Trim().ToUpperInvariant()
                : cell.ShortLabel.Trim().ToUpperInvariant();
            ownerTokenByCoordinate[cell.Column, cell.Row] = ownerToken;
            if (!PanelCladdingKeyService.IsCellLabelToken(value))
            {
                materialByToken[ownerToken] = _keys.NormalizeCladdingValue(value);
            }
        }
        AssignBlankComponents(ownerTokenByCoordinate, materialByToken);

        var ownerIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var materialByOwner = new Dictionary<int, string>();
        var ownerByCoordinate = new int[columnCount, rowCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                string token = ownerTokenByCoordinate[column, row];
                if (!ownerIds.TryGetValue(token, out int ownerId))
                {
                    ownerId = ownerIds.Count;
                    ownerIds[token] = ownerId;
                    materialByOwner[ownerId] = materialByToken.GetValueOrDefault(token, string.Empty);
                }
                ownerByCoordinate[column, row] = ownerId;
            }
        }

        if (!SourceMissingSegmentsAreValid(
            source.Topology,
            ownerByCoordinate,
            columnCount,
            rowCount,
            out string invalidSegment))
        {
            return OperationResponse<SourcePartition>.Fail(
                $"PANEL_CLADDING_MATCH_SOURCE_BOUNDARY_UNSUPPORTED: {invalidSegment}");
        }

        int[] columnBands = BuildColumnBands(ownerByCoordinate, columnCount, rowCount);
        int[] rowBands = BuildRowBands(ownerByCoordinate, columnCount, rowCount);
        int columnBandCount = columnBands[^1] + 1;
        int rowBandCount = rowBands[^1] + 1;
        var ownerByBand = new int[columnBandCount, rowBandCount];
        var assignedBands = new bool[columnBandCount, rowBandCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                int bandColumn = columnBands[column];
                int bandRow = rowBands[row];
                int owner = ownerByCoordinate[column, row];
                if (assignedBands[bandColumn, bandRow] &&
                    ownerByBand[bandColumn, bandRow] != owner)
                {
                    return OperationResponse<SourcePartition>.Fail(
                        "PANEL_CLADDING_MATCH_SOURCE_PARTITION_COLLAPSE_INVALID");
                }
                ownerByBand[bandColumn, bandRow] = owner;
                assignedBands[bandColumn, bandRow] = true;
            }
        }
        return OperationResponse<SourcePartition>.Ok(new SourcePartition
        {
            ColumnBandCount = columnBandCount,
            RowBandCount = rowBandCount,
            OwnerByBand = ownerByBand,
            MaterialByOwner = materialByOwner,
            SourceColumnBands = columnBands,
            SourceRowBands = rowBands
        });
    }

    private static void AssignBlankComponents(
        string[,] owners,
        IDictionary<string, string> materialByToken)
    {
        int columnCount = owners.GetLength(0);
        int rowCount = owners.GetLength(1);
        int blankIndex = 0;
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                if (!string.IsNullOrEmpty(owners[column, row]))
                {
                    continue;
                }
                string token = $"#BLANK{blankIndex++}";
                materialByToken[token] = string.Empty;
                var pending = new Queue<(int Column, int Row)>();
                pending.Enqueue((column, row));
                while (pending.Count > 0)
                {
                    (int currentColumn, int currentRow) = pending.Dequeue();
                    if (!string.IsNullOrEmpty(owners[currentColumn, currentRow]))
                    {
                        continue;
                    }
                    owners[currentColumn, currentRow] = token;
                    foreach ((int nextColumn, int nextRow) in Neighbors(
                        currentColumn,
                        currentRow,
                        columnCount,
                        rowCount))
                    {
                        if (string.IsNullOrEmpty(owners[nextColumn, nextRow]))
                        {
                            pending.Enqueue((nextColumn, nextRow));
                        }
                    }
                }
            }
        }
    }

    private static bool SourceMissingSegmentsAreValid(
        PanelCladdingTopologyState topology,
        int[,] ownerByCoordinate,
        int columnCount,
        int rowCount,
        out string invalidSegment)
    {
        foreach (PanelCladdingSegmentCoordinate segment in topology.MissingSegments)
        {
            (int FirstColumn, int FirstRow, int SecondColumn, int SecondRow) coordinates =
                segment.Axis == PanelCladdingTopologyAxis.Horizontal
                    ? (segment.Bay, segment.Track, segment.Bay, segment.Track + 1)
                    : (segment.Track, segment.Bay, segment.Track + 1, segment.Bay);
            if (coordinates.FirstColumn < 0 || coordinates.SecondColumn >= columnCount ||
                coordinates.FirstRow < 0 || coordinates.SecondRow >= rowCount ||
                ownerByCoordinate[coordinates.FirstColumn, coordinates.FirstRow] !=
                ownerByCoordinate[coordinates.SecondColumn, coordinates.SecondRow])
            {
                invalidSegment = $"{segment.Axis}:{segment.Track}:{segment.Bay}";
                return false;
            }
        }
        invalidSegment = string.Empty;
        return true;
    }

    private static int[] BuildColumnBands(
        int[,] ownerByCoordinate,
        int columnCount,
        int rowCount)
    {
        var bands = new int[columnCount];
        for (int column = 1; column < columnCount; column++)
        {
            bool essential = Enumerable.Range(0, rowCount).Any(row =>
                ownerByCoordinate[column - 1, row] != ownerByCoordinate[column, row]);
            bands[column] = bands[column - 1] + (essential ? 1 : 0);
        }
        return bands;
    }

    private static int[] BuildRowBands(
        int[,] ownerByCoordinate,
        int columnCount,
        int rowCount)
    {
        var bands = new int[rowCount];
        for (int row = 1; row < rowCount; row++)
        {
            bool essential = Enumerable.Range(0, columnCount).Any(column =>
                ownerByCoordinate[column, row - 1] != ownerByCoordinate[column, row]);
            bands[row] = bands[row - 1] + (essential ? 1 : 0);
        }
        return bands;
    }

    private static bool TargetMissingSegmentsAreRepresentable(
        PanelCladdingTopologyState topology,
        SourcePartition partition,
        IReadOnlyList<int> columnMap,
        IReadOnlyList<int> rowMap,
        int targetColumnCount,
        int targetRowCount)
    {
        foreach (PanelCladdingSegmentCoordinate segment in topology.MissingSegments)
        {
            if (segment.Axis == PanelCladdingTopologyAxis.Horizontal)
            {
                if (segment.Track < 0 || segment.Track + 1 >= targetRowCount ||
                    segment.Bay < 0 || segment.Bay >= targetColumnCount ||
                    partition.OwnerByBand[
                        columnMap[segment.Bay],
                        rowMap[segment.Track]] !=
                    partition.OwnerByBand[
                        columnMap[segment.Bay],
                        rowMap[segment.Track + 1]])
                {
                    return false;
                }
            }
            else if (segment.Track < 0 || segment.Track + 1 >= targetColumnCount ||
                segment.Bay < 0 || segment.Bay >= targetRowCount ||
                partition.OwnerByBand[
                    columnMap[segment.Track],
                    rowMap[segment.Bay]] !=
                partition.OwnerByBand[
                    columnMap[segment.Track + 1],
                    rowMap[segment.Bay]])
            {
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<int[]> EnumerateBandMaps(
        int targetCount,
        int sourceBandCount)
    {
        if (sourceBandCount <= 0 || targetCount < sourceBandCount)
        {
            yield break;
        }
        var lengths = new int[sourceBandCount];
        foreach (int[] map in EnumerateLengths(0, targetCount))
        {
            yield return map;
        }

        IEnumerable<int[]> EnumerateLengths(int band, int remaining)
        {
            if (band == sourceBandCount - 1)
            {
                lengths[band] = remaining;
                yield return Expand(lengths, targetCount);
                yield break;
            }
            int remainingBands = sourceBandCount - band - 1;
            for (int length = remaining - remainingBands; length >= 1; length--)
            {
                lengths[band] = length;
                foreach (int[] map in EnumerateLengths(band + 1, remaining - length))
                {
                    yield return map;
                }
            }
        }
    }

    private static IEnumerable<int[]> EnumerateCandidateMaps(
        int targetCount,
        int sourceBandCount,
        IReadOnlyList<int> sourcePhysicalBands)
    {
        int[]? preferred = sourcePhysicalBands.Count == targetCount
            ? sourcePhysicalBands.ToArray()
            : null;
        if (preferred is not null)
        {
            yield return preferred;
        }
        foreach (int[] map in EnumerateBandMaps(targetCount, sourceBandCount))
        {
            if (preferred is null || !map.SequenceEqual(preferred))
            {
                yield return map;
            }
        }
    }

    private static int[] Expand(IReadOnlyList<int> lengths, int targetCount)
    {
        var result = new int[targetCount];
        int index = 0;
        for (int band = 0; band < lengths.Count; band++)
        {
            for (int item = 0; item < lengths[band]; item++)
            {
                result[index++] = band;
            }
        }
        return result;
    }

    private static IEnumerable<(int Column, int Row)> Neighbors(
        int column,
        int row,
        int columnCount,
        int rowCount)
    {
        if (column > 0) yield return (column - 1, row);
        if (column + 1 < columnCount) yield return (column + 1, row);
        if (row > 0) yield return (column, row - 1);
        if (row + 1 < rowCount) yield return (column, row + 1);
    }

    private sealed class SourcePartition
    {
        public int ColumnBandCount { get; init; }
        public int RowBandCount { get; init; }
        public int[,] OwnerByBand { get; init; } = new int[0, 0];
        public IReadOnlyDictionary<int, string> MaterialByOwner { get; init; } =
            new Dictionary<int, string>();
        public IReadOnlyList<int> SourceColumnBands { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> SourceRowBands { get; init; } = Array.Empty<int>();
    }
}
