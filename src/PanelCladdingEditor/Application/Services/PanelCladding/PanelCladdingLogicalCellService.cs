using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingLogicalCellService
{
    public IReadOnlyDictionary<string, string> Expand(
        IReadOnlyList<PanelCladdingCell> cells,
        PanelCladdingTopologyState topology,
        IReadOnlyDictionary<string, string> logicalValues)
    {
        PanelCladdingCell[][] groups = BuildGroups(cells, topology);
        var representativeByLabel = BuildRepresentativeMap(groups);
        var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell[] group in groups)
        {
            PanelCladdingCell representative = group[0];
            string value = logicalValues.GetValueOrDefault(
                representative.UserTextKey,
                representative.Value).Trim();
            if (PanelCladdingKeyService.IsCellLabelToken(value) &&
                representativeByLabel.TryGetValue(value, out PanelCladdingCell? target))
            {
                value = target.ShortLabel;
            }
            foreach (PanelCladdingCell member in group)
            {
                expanded[member.UserTextKey] = value.Length == 0 ||
                    string.Equals(member.UserTextKey, representative.UserTextKey, StringComparison.OrdinalIgnoreCase)
                    ? value
                    : representative.ShortLabel;
            }
        }
        return expanded;
    }

    public IReadOnlyDictionary<string, string> Collapse(
        IReadOnlyList<PanelCladdingCell> cells,
        PanelCladdingTopologyState topology,
        IReadOnlyDictionary<string, string> normalizedPhysicalValues)
    {
        PanelCladdingCell[][] groups = BuildGroups(cells, topology);
        var representativeByLabel = BuildRepresentativeMap(groups);

        var logicalValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell[] group in groups)
        {
            PanelCladdingCell representative = group[0];
            string value = normalizedPhysicalValues.GetValueOrDefault(
                representative.UserTextKey,
                string.Empty);
            if (PanelCladdingKeyService.IsCellLabelToken(value) &&
                representativeByLabel.TryGetValue(value, out PanelCladdingCell? target))
            {
                value = target.ShortLabel;
            }
            logicalValues[representative.UserTextKey] = value;
        }
        return logicalValues;
    }

    private static PanelCladdingCell[][] BuildGroups(
        IReadOnlyList<PanelCladdingCell> cells,
        PanelCladdingTopologyState topology)
    {
        PanelCladdingCell[] orderedCells = cells
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToArray();
        if (orderedCells.Length == 0)
        {
            return Array.Empty<PanelCladdingCell[]>();
        }

        int[] parents = Enumerable.Range(0, orderedCells.Length).ToArray();
        var indexesByCoordinate = orderedCells
            .Select((cell, index) => (cell, index))
            .ToDictionary(item => (item.cell.Column, item.cell.Row), item => item.index);
        foreach (PanelCladdingSegmentCoordinate missing in topology.MissingSegments)
        {
            (int Column, int Row) first = missing.Axis == PanelCladdingTopologyAxis.Horizontal
                ? (missing.Bay, missing.Track)
                : (missing.Track, missing.Bay);
            (int Column, int Row) second = missing.Axis == PanelCladdingTopologyAxis.Horizontal
                ? (missing.Bay, missing.Track + 1)
                : (missing.Track + 1, missing.Bay);
            if (indexesByCoordinate.TryGetValue(first, out int firstIndex) &&
                indexesByCoordinate.TryGetValue(second, out int secondIndex))
            {
                Union(parents, firstIndex, secondIndex);
            }
        }
        return orderedCells
            .Select((cell, index) => (cell, Root: Find(parents, index)))
            .GroupBy(item => item.Root)
            .Select(group => group
                .Select(item => item.cell)
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToArray())
            .OrderBy(group => group[0].Row)
            .ThenBy(group => group[0].Column)
            .ToArray();
    }

    private static Dictionary<string, PanelCladdingCell> BuildRepresentativeMap(
        IEnumerable<PanelCladdingCell[]> groups)
    {
        var representativeByLabel = new Dictionary<string, PanelCladdingCell>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell[] group in groups)
        {
            PanelCladdingCell representative = group[0];
            foreach (PanelCladdingCell member in group)
            {
                representativeByLabel[member.ShortLabel] = representative;
            }
        }
        return representativeByLabel;
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
