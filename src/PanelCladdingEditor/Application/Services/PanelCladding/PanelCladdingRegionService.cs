using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingRegionService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingRegionService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingRegionSet> Resolve(
        IReadOnlyList<PanelCladdingCell> cells,
        IReadOnlyDictionary<string, string>? requestedValues = null,
        bool requirePopulatedCells = false)
    {
        PanelCladdingCell[] orderedCells = (cells ?? Array.Empty<PanelCladdingCell>())
            .OrderBy(cell => cell.Column)
            .ThenBy(cell => cell.Row)
            .ToArray();
        if (orderedCells.Length == 0)
        {
            return OperationResponse<PanelCladdingRegionSet>.Fail("PANEL_CLADDING_CELLS_REQUIRED");
        }

        var cellsByLabel = new Dictionary<string, PanelCladdingCell>(StringComparer.OrdinalIgnoreCase);
        var valuesByLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in orderedCells)
        {
            if (!cellsByLabel.TryAdd(cell.ShortLabel, cell))
            {
                return OperationResponse<PanelCladdingRegionSet>.Fail(
                    $"PANEL_CLADDING_DUPLICATE_CELL_LABEL: {cell.ShortLabel}");
            }
            string? requested = null;
            requestedValues?.TryGetValue(cell.UserTextKey, out requested);
            string value = _keys.NormalizeCladdingValue(requested ?? cell.Value);
            if (requirePopulatedCells && value.Length == 0)
            {
                return OperationResponse<PanelCladdingRegionSet>.Fail(
                    $"PANEL_CLADDING_CELL_REQUIRED: {cell.ShortLabel} ({cell.UserTextKey}) is blank.");
            }
            valuesByLabel[cell.ShortLabel] = value;
        }

        var resolvedOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in orderedCells)
        {
            OperationResponse<string> owner = ResolveOwner(
                cell.ShortLabel,
                cellsByLabel,
                valuesByLabel,
                resolvedOwners,
                resolving);
            if (!owner.Success)
            {
                return OperationResponse<PanelCladdingRegionSet>.Fail(owner.Message);
            }
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var regions = new List<PanelCladdingRegion>();
        foreach (IGrouping<string, PanelCladdingCell> group in orderedCells
            .Where(cell => resolvedOwners[cell.ShortLabel].Length > 0)
            .GroupBy(cell => resolvedOwners[cell.ShortLabel], StringComparer.OrdinalIgnoreCase))
        {
            PanelCladdingCell[] regionCells = group
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToArray();
            if (!IsEdgeConnected(regionCells))
            {
                return OperationResponse<PanelCladdingRegionSet>.Fail(
                    $"PANEL_CLADDING_REGION_DISCONNECTED: {string.Join(",", regionCells.Select(cell => cell.ShortLabel))}");
            }

            string originalOwner = group.Key;
            string material = valuesByLabel[originalOwner];
            if (material.Length == 0 || PanelCladdingKeyService.IsCellLabelToken(material))
            {
                return OperationResponse<PanelCladdingRegionSet>.Fail(
                    $"PANEL_CLADDING_REGION_MATERIAL_REQUIRED: {originalOwner}");
            }

            PanelCladdingCell canonicalOwner = regionCells[0];
            foreach (PanelCladdingCell cell in regionCells)
            {
                normalized[cell.UserTextKey] = string.Equals(
                    cell.ShortLabel,
                    canonicalOwner.ShortLabel,
                    StringComparison.OrdinalIgnoreCase)
                    ? material
                    : canonicalOwner.ShortLabel;
            }
            regions.Add(new PanelCladdingRegion
            {
                OwnerCellLabel = canonicalOwner.ShortLabel,
                MaterialCode = material,
                Cells = regionCells
            });
        }

        foreach (PanelCladdingCell blankCell in orderedCells.Where(cell =>
            resolvedOwners[cell.ShortLabel].Length == 0))
        {
            normalized[blankCell.UserTextKey] = string.Empty;
        }

        return OperationResponse<PanelCladdingRegionSet>.Ok(new PanelCladdingRegionSet
        {
            Regions = regions
                .OrderBy(region => region.Cells[0].Column)
                .ThenBy(region => region.Cells[0].Row)
                .ToArray(),
            NormalizedCellValues = normalized
        });
    }

    private static OperationResponse<string> ResolveOwner(
        string label,
        IReadOnlyDictionary<string, PanelCladdingCell> cellsByLabel,
        IReadOnlyDictionary<string, string> valuesByLabel,
        IDictionary<string, string> resolvedOwners,
        ISet<string> resolving)
    {
        if (resolvedOwners.TryGetValue(label, out string? cached))
        {
            return OperationResponse<string>.Ok(cached);
        }
        if (!resolving.Add(label))
        {
            return OperationResponse<string>.Fail($"PANEL_CLADDING_REFERENCE_CYCLE: {label}");
        }

        string value = valuesByLabel[label];
        if (value.Length == 0)
        {
            resolving.Remove(label);
            resolvedOwners[label] = string.Empty;
            return OperationResponse<string>.Ok(string.Empty);
        }
        if (!PanelCladdingKeyService.IsCellLabelToken(value))
        {
            resolving.Remove(label);
            resolvedOwners[label] = label;
            return OperationResponse<string>.Ok(label);
        }
        if (!cellsByLabel.TryGetValue(value, out PanelCladdingCell? target))
        {
            resolving.Remove(label);
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_REFERENCE_TARGET_NOT_FOUND: {label} references {value}.");
        }

        OperationResponse<string> owner = ResolveOwner(
            target.ShortLabel,
            cellsByLabel,
            valuesByLabel,
            resolvedOwners,
            resolving);
        resolving.Remove(label);
        if (!owner.Success)
        {
            return owner;
        }
        string ownerLabel = owner.Data ?? string.Empty;
        if (ownerLabel.Length == 0)
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_REFERENCE_TARGET_BLANK: {label} references {target.ShortLabel}.");
        }
        resolvedOwners[label] = ownerLabel;
        return OperationResponse<string>.Ok(ownerLabel);
    }

    private static bool IsEdgeConnected(IReadOnlyList<PanelCladdingCell> cells)
    {
        if (cells.Count <= 1)
        {
            return true;
        }
        var coordinates = cells
            .Select(cell => (cell.Column, cell.Row))
            .ToHashSet();
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
}
