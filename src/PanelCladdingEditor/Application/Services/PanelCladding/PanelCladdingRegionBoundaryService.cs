using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public enum PanelCladdingCellBoundarySide
{
    Bottom,
    Right,
    Top,
    Left
}

public sealed record PanelCladdingRegionBoundarySegment(
    PanelCladdingCell Cell,
    PanelCladdingCellBoundarySide Side,
    bool IsPanelPerimeter);

public sealed class PanelCladdingRegionBoundaryService
{
    public OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>> CreateBoundary(
        IReadOnlyList<PanelCladdingCell> panelCells,
        IReadOnlyList<PanelCladdingCell> regionCells)
    {
        if (panelCells is null || panelCells.Count == 0)
        {
            return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                "PANEL_CLADDING_BOUNDARY_PANEL_CELLS_REQUIRED");
        }
        if (regionCells is null || regionCells.Count == 0)
        {
            return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                "PANEL_CLADDING_BOUNDARY_REGION_CELLS_REQUIRED");
        }

        var panelByCoordinate = new Dictionary<CellCoordinate, PanelCladdingCell>();
        var panelLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in panelCells)
        {
            if (!panelByCoordinate.TryAdd(new CellCoordinate(cell.Column, cell.Row), cell) ||
                !panelLabels.Add(cell.ShortLabel))
            {
                return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                    $"PANEL_CLADDING_BOUNDARY_DUPLICATE_PANEL_CELL: {cell.ShortLabel}");
            }
        }

        var regionCoordinates = new HashSet<CellCoordinate>();
        var boundary = new Dictionary<GridEdge, BoundaryCandidate>();
        foreach (PanelCladdingCell cell in regionCells)
        {
            var coordinate = new CellCoordinate(cell.Column, cell.Row);
            if (!panelByCoordinate.TryGetValue(coordinate, out PanelCladdingCell? panelCell) ||
                !string.Equals(panelCell.ShortLabel, cell.ShortLabel, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                    $"PANEL_CLADDING_BOUNDARY_REGION_CELL_UNKNOWN: {cell.ShortLabel}");
            }
            if (!regionCoordinates.Add(coordinate))
            {
                return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                    $"PANEL_CLADDING_BOUNDARY_DUPLICATE_REGION_CELL: {cell.ShortLabel}");
            }

            AddOrCancel(boundary, cell, PanelCladdingCellBoundarySide.Bottom,
                new GridPoint(cell.Column, cell.Row),
                new GridPoint(cell.Column + 1, cell.Row));
            AddOrCancel(boundary, cell, PanelCladdingCellBoundarySide.Right,
                new GridPoint(cell.Column + 1, cell.Row),
                new GridPoint(cell.Column + 1, cell.Row + 1));
            AddOrCancel(boundary, cell, PanelCladdingCellBoundarySide.Top,
                new GridPoint(cell.Column + 1, cell.Row + 1),
                new GridPoint(cell.Column, cell.Row + 1));
            AddOrCancel(boundary, cell, PanelCladdingCellBoundarySide.Left,
                new GridPoint(cell.Column, cell.Row + 1),
                new GridPoint(cell.Column, cell.Row));
        }

        PanelCladdingRegionBoundarySegment[] result = boundary.Values
            .Select(candidate => new PanelCladdingRegionBoundarySegment(
                candidate.Cell,
                candidate.Side,
                IsPanelPerimeter(panelByCoordinate, candidate.Cell, candidate.Side)))
            .OrderBy(segment => segment.Cell.Column)
            .ThenBy(segment => segment.Cell.Row)
            .ThenBy(segment => segment.Side)
            .ToArray();
        if (result.Length == 0)
        {
            return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Fail(
                "PANEL_CLADDING_BOUNDARY_EMPTY");
        }
        return OperationResponse<IReadOnlyList<PanelCladdingRegionBoundarySegment>>.Ok(result);
    }

    private static void AddOrCancel(
        IDictionary<GridEdge, BoundaryCandidate> boundary,
        PanelCladdingCell cell,
        PanelCladdingCellBoundarySide side,
        GridPoint start,
        GridPoint end)
    {
        GridEdge edge = GridEdge.Create(start, end);
        if (!boundary.Remove(edge))
        {
            boundary.Add(edge, new BoundaryCandidate(cell, side));
        }
    }

    private static bool IsPanelPerimeter(
        IReadOnlyDictionary<CellCoordinate, PanelCladdingCell> panelByCoordinate,
        PanelCladdingCell cell,
        PanelCladdingCellBoundarySide side)
    {
        CellCoordinate neighbor = side switch
        {
            PanelCladdingCellBoundarySide.Bottom => new CellCoordinate(cell.Column, cell.Row - 1),
            PanelCladdingCellBoundarySide.Right => new CellCoordinate(cell.Column + 1, cell.Row),
            PanelCladdingCellBoundarySide.Top => new CellCoordinate(cell.Column, cell.Row + 1),
            PanelCladdingCellBoundarySide.Left => new CellCoordinate(cell.Column - 1, cell.Row),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
        };
        return !panelByCoordinate.ContainsKey(neighbor);
    }

    private sealed record BoundaryCandidate(
        PanelCladdingCell Cell,
        PanelCladdingCellBoundarySide Side);

    private readonly record struct CellCoordinate(int Column, int Row);

    private readonly record struct GridPoint(int ColumnBoundary, int RowBoundary);

    private readonly record struct GridEdge(GridPoint First, GridPoint Second)
    {
        public static GridEdge Create(GridPoint first, GridPoint second) =>
            Compare(first, second) <= 0
                ? new GridEdge(first, second)
                : new GridEdge(second, first);

        private static int Compare(GridPoint left, GridPoint right)
        {
            int column = left.ColumnBoundary.CompareTo(right.ColumnBoundary);
            return column != 0 ? column : left.RowBoundary.CompareTo(right.RowBoundary);
        }
    }
}
