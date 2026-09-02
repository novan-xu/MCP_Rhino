using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public sealed class PanelCladdingCellGroup
{
    private readonly HashSet<string> _memberKeys;
    private readonly HashSet<(int Column, int Row)> _coordinates;

    internal PanelCladdingCellGroup(IReadOnlyList<PanelCladdingCell> cells)
    {
        Cells = cells;
        Representative = cells
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .First();
        MinimumColumn = cells.Min(cell => cell.Column);
        MaximumColumn = cells.Max(cell => cell.Column);
        MinimumRow = cells.Min(cell => cell.Row);
        MaximumRow = cells.Max(cell => cell.Row);
        IsRectangular = cells.Count ==
            (MaximumColumn - MinimumColumn + 1) * (MaximumRow - MinimumRow + 1);
        _memberKeys = cells
            .Select(cell => cell.UserTextKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _coordinates = cells
            .Select(cell => (cell.Column, cell.Row))
            .ToHashSet();
    }

    public PanelCladdingCell Representative { get; }

    public IReadOnlyList<PanelCladdingCell> Cells { get; }

    public int MinimumColumn { get; }

    public int MaximumColumn { get; }

    public int MinimumRow { get; }

    public int MaximumRow { get; }

    public bool IsRectangular { get; }

    public bool Contains(string userTextKey) => _memberKeys.Contains(userTextKey);

    public bool Contains(int column, int row) => _coordinates.Contains((column, row));
}

public sealed class PanelCladdingCellTopology
{
    private readonly IReadOnlyDictionary<string, PanelCladdingCellGroup> _groupsByCellKey;
    private readonly IReadOnlyDictionary<string, PanelCladdingCellGroup> _groupsByLabel;

    private PanelCladdingCellTopology(
        IReadOnlyList<PanelCladdingCellGroup> groups,
        IReadOnlyDictionary<string, PanelCladdingCellGroup> groupsByCellKey,
        IReadOnlyDictionary<string, PanelCladdingCellGroup> groupsByLabel)
    {
        Groups = groups;
        _groupsByCellKey = groupsByCellKey;
        _groupsByLabel = groupsByLabel;
    }

    public IReadOnlyList<PanelCladdingCellGroup> Groups { get; }

    public PanelCladdingCellGroup? FindByCellKey(string? userTextKey) =>
        !string.IsNullOrWhiteSpace(userTextKey) && _groupsByCellKey.TryGetValue(userTextKey, out PanelCladdingCellGroup? group)
            ? group
            : null;

    public PanelCladdingCellGroup? FindByLabel(string? shortLabel) =>
        !string.IsNullOrWhiteSpace(shortLabel) && _groupsByLabel.TryGetValue(shortLabel, out PanelCladdingCellGroup? group)
            ? group
            : null;

    public static PanelCladdingCellTopology Build(
        PanelCladdingLayout layout,
        IReadOnlySet<string> deletedAtomicIds)
    {
        PanelCladdingCell[] cells = layout.Cells.ToArray();
        if (cells.Length == 0)
        {
            return new PanelCladdingCellTopology(
                Array.Empty<PanelCladdingCellGroup>(),
                new Dictionary<string, PanelCladdingCellGroup>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, PanelCladdingCellGroup>(StringComparer.OrdinalIgnoreCase));
        }

        int[] parents = Enumerable.Range(0, cells.Length).ToArray();
        var cellIndexes = cells
            .Select((cell, index) => (cell, index))
            .ToDictionary(item => (item.cell.Column, item.cell.Row), item => item.index);

        foreach (string id in deletedAtomicIds)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(
                    id,
                    out PanelExtrusionAxis axis,
                    out double offset,
                    out int bay))
            {
                continue;
            }

            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? layout.HorizontalOffsets
                : layout.VerticalOffsets;
            int line = IndexOfOffset(offsets, offset);
            if (line < 0)
            {
                continue;
            }

            (int Column, int Row) first = axis == PanelExtrusionAxis.Horizontal
                ? (bay, line)
                : (line, bay);
            (int Column, int Row) second = axis == PanelExtrusionAxis.Horizontal
                ? (bay, line + 1)
                : (line + 1, bay);
            if (cellIndexes.TryGetValue(first, out int firstIndex) &&
                cellIndexes.TryGetValue(second, out int secondIndex))
            {
                Union(parents, firstIndex, secondIndex);
            }
        }

        PanelCladdingCellGroup[] groups = cells
            .Select((cell, index) => (cell, Root: Find(parents, index)))
            .GroupBy(item => item.Root)
            .Select(group => new PanelCladdingCellGroup(group
                .Select(item => item.cell)
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToArray()))
            .OrderBy(group => group.Representative.Row)
            .ThenBy(group => group.Representative.Column)
            .ToArray();

        var byCellKey = new Dictionary<string, PanelCladdingCellGroup>(StringComparer.OrdinalIgnoreCase);
        var byLabel = new Dictionary<string, PanelCladdingCellGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCellGroup group in groups)
        {
            foreach (PanelCladdingCell cell in group.Cells)
            {
                byCellKey[cell.UserTextKey] = group;
                byLabel[cell.ShortLabel] = group;
            }
        }

        return new PanelCladdingCellTopology(groups, byCellKey, byLabel);
    }

    private static int IndexOfOffset(IReadOnlyList<double> values, double target)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (Math.Abs(values[index] - target) < 1e-5d)
            {
                return index;
            }
        }
        return -1;
    }

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
}
