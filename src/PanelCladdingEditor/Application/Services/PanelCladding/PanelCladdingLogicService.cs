using System.Text.Json;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingLogicService
{
    public OperationResponse<string> Encode(
        IReadOnlyList<PanelCladdingCell> cells,
        IReadOnlyDictionary<string, string> cellValues)
    {
        PanelCladdingCell[] orderedCells = (cells ?? Array.Empty<PanelCladdingCell>())
            .OrderBy(cell => cell.Column)
            .ThenBy(cell => cell.Row)
            .ToArray();
        if (orderedCells.Length == 0)
        {
            return OperationResponse<string>.Fail("PANEL_CLADDING_LOGIC_CELLS_REQUIRED");
        }

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in orderedCells)
        {
            string label = NormalizeLabel(cell.ShortLabel);
            if (!PanelCladdingKeyService.IsCellLabelToken(label) || !labels.Add(label))
            {
                return OperationResponse<string>.Fail(
                    $"PANEL_CLADDING_LOGIC_CELL_INVALID: {cell.ShortLabel}");
            }
        }

        var logic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in orderedCells)
        {
            string value = GetValue(cellValues, cell.UserTextKey);
            string parent = PanelCladdingKeyService.IsCellLabelToken(value)
                ? NormalizeLabel(value)
                : string.Empty;
            if (parent.Length > 0 && !labels.Contains(parent))
            {
                return OperationResponse<string>.Fail(
                    $"PANEL_CLADDING_LOGIC_PARENT_UNKNOWN: {cell.ShortLabel}={parent}");
            }
            logic[NormalizeLabel(cell.ShortLabel)] = parent;
        }

        OperationResponse validated = ValidateGraph(logic);
        return validated.Success
            ? OperationResponse<string>.Ok(JsonSerializer.Serialize(logic))
            : OperationResponse<string>.Fail(validated.Message);
    }

    public OperationResponse<IReadOnlyDictionary<string, string>> Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return OperationResponse<IReadOnlyDictionary<string, string>>.Ok(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var logic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                    "PANEL_CLADDING_LOGIC_OBJECT_REQUIRED");
            }
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                string label = NormalizeLabel(property.Name);
                if (!PanelCladdingKeyService.IsCellLabelToken(label) || logic.ContainsKey(label))
                {
                    return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                        $"PANEL_CLADDING_LOGIC_CELL_INVALID: {property.Name}");
                }
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                        $"PANEL_CLADDING_LOGIC_VALUE_INVALID: {property.Name}");
                }
                string parent = NormalizeLabel(property.Value.GetString() ?? string.Empty);
                if (parent.Length > 0 && !PanelCladdingKeyService.IsCellLabelToken(parent))
                {
                    return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                        $"PANEL_CLADDING_LOGIC_PARENT_INVALID: {property.Name}={parent}");
                }
                logic[label] = parent;
            }
        }
        catch (JsonException ex)
        {
            return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                $"PANEL_CLADDING_LOGIC_JSON_INVALID: {ex.Message}");
        }

        OperationResponse validated = ValidateGraph(logic);
        return validated.Success
            ? OperationResponse<IReadOnlyDictionary<string, string>>.Ok(logic)
            : OperationResponse<IReadOnlyDictionary<string, string>>.Fail(validated.Message);
    }

    public OperationResponse<string> ResolveOwnerLabel(
        IReadOnlyList<string> coveredCellLabels,
        IReadOnlyDictionary<string, string> logic)
    {
        string[] coverage = (coveredCellLabels ?? Array.Empty<string>())
            .Select(NormalizeLabel)
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (coverage.Length == 0 || logic is null || logic.Count == 0 ||
            coverage.Any(label => !logic.ContainsKey(label)))
        {
            return OperationResponse<string>.Ok(string.Empty);
        }

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string label in coverage)
        {
            OperationResponse<string> root = ResolveRoot(label, logic);
            if (!root.Success || root.Data is null)
            {
                return OperationResponse<string>.Fail(root.Message);
            }
            roots.Add(root.Data);
        }
        if (roots.Count != 1)
        {
            return OperationResponse<string>.Ok(string.Empty);
        }

        string owner = roots.Single();
        return OperationResponse<string>.Ok(coverage.Contains(owner, StringComparer.OrdinalIgnoreCase)
            ? owner
            : string.Empty);
    }

    private static OperationResponse ValidateGraph(
        IReadOnlyDictionary<string, string> logic)
    {
        foreach ((string label, string parent) in logic)
        {
            if (parent.Length > 0 && !logic.ContainsKey(parent))
            {
                return OperationResponse.Fail(
                    $"PANEL_CLADDING_LOGIC_PARENT_UNKNOWN: {label}={parent}");
            }
            OperationResponse<string> root = ResolveRoot(label, logic);
            if (!root.Success)
            {
                return OperationResponse.Fail(root.Message);
            }
        }
        return OperationResponse.Ok();
    }

    private static OperationResponse<string> ResolveRoot(
        string label,
        IReadOnlyDictionary<string, string> logic)
    {
        string current = NormalizeLabel(label);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            if (!visited.Add(current))
            {
                return OperationResponse<string>.Fail(
                    $"PANEL_CLADDING_LOGIC_REFERENCE_CYCLE: {label}");
            }
            if (!logic.TryGetValue(current, out string? parent))
            {
                return OperationResponse<string>.Fail(
                    $"PANEL_CLADDING_LOGIC_PARENT_UNKNOWN: {current}");
            }
            parent = NormalizeLabel(parent);
            if (parent.Length == 0)
            {
                return OperationResponse<string>.Ok(current);
            }
            current = parent;
        }
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string requestedKey)
    {
        if (values is null)
        {
            return string.Empty;
        }
        if (values.TryGetValue(requestedKey, out string? exact))
        {
            return exact ?? string.Empty;
        }
        return values.FirstOrDefault(item => string.Equals(
            item.Key,
            requestedKey,
            StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
    }

    private static string NormalizeLabel(string? label) =>
        (label ?? string.Empty).Trim().ToUpperInvariant();
}
