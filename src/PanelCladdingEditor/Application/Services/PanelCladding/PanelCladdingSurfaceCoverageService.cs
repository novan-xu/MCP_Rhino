using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSurfaceCoverageService
{
    public const string UserTextKey = "Merge_Mark";
    public const string LegacyUserTextKey = "CW_1.03_CLADDING_CELLS";

    public OperationResponse<string> ResolveStoredValue(
        string? canonicalValue,
        string? legacyValue)
    {
        bool hasCanonical = !string.IsNullOrWhiteSpace(canonicalValue);
        bool hasLegacy = !string.IsNullOrWhiteSpace(legacyValue);
        if (!hasCanonical)
        {
            return OperationResponse<string>.Ok(hasLegacy ? legacyValue! : string.Empty);
        }
        if (!hasLegacy)
        {
            return OperationResponse<string>.Ok(canonicalValue!);
        }

        OperationResponse<IReadOnlyList<string>> canonical = Decode(canonicalValue);
        OperationResponse<IReadOnlyList<string>> legacy = Decode(legacyValue);
        if (!canonical.Success || canonical.Data is null ||
            !legacy.Success || legacy.Data is null ||
            !canonical.Data.SequenceEqual(legacy.Data, StringComparer.OrdinalIgnoreCase))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_COVERAGE_KEY_CONFLICT: " +
                $"{UserTextKey} and {LegacyUserTextKey} differ.");
        }
        return OperationResponse<string>.Ok(canonicalValue!);
    }

    public OperationResponse<string> Encode(
        string ownerCellLabel,
        IEnumerable<PanelCladdingCell> cells)
    {
        string owner = Normalize(ownerCellLabel);
        PanelCladdingCell[] ordered = (cells ?? Array.Empty<PanelCladdingCell>())
            .OrderBy(cell => cell.Column)
            .ThenBy(cell => cell.Row)
            .ToArray();
        if (!PanelCladdingKeyService.IsCellLabelToken(owner) || ordered.Length == 0)
        {
            return OperationResponse<string>.Fail(
                "PANEL_CLADDING_SURFACE_COVERAGE_OWNER_REQUIRED");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = new List<string>(ordered.Length);
        foreach (PanelCladdingCell cell in ordered)
        {
            string label = Normalize(cell.ShortLabel);
            if (!PanelCladdingKeyService.IsCellLabelToken(label) || !seen.Add(label))
            {
                return OperationResponse<string>.Fail(
                    $"PANEL_CLADDING_SURFACE_COVERAGE_CELL_INVALID: {cell.ShortLabel}");
            }
            if (!string.Equals(label, owner, StringComparison.OrdinalIgnoreCase))
            {
                remaining.Add(label);
            }
        }
        if (!seen.Contains(owner))
        {
            return OperationResponse<string>.Fail(
                $"PANEL_CLADDING_SURFACE_COVERAGE_OWNER_MISSING: {owner}");
        }
        return OperationResponse<string>.Ok(string.Join(';', new[] { owner }.Concat(remaining)));
    }

    public OperationResponse<IReadOnlyList<string>> Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return OperationResponse<IReadOnlyList<string>>.Fail(
                "PANEL_CLADDING_SURFACE_COVERAGE_REQUIRED");
        }

        string[] supplied = payload.Split(';', StringSplitOptions.None);
        var labels = new List<string>(supplied.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string token in supplied)
        {
            string label = Normalize(token);
            if (!PanelCladdingKeyService.IsCellLabelToken(label) || !seen.Add(label))
            {
                return OperationResponse<IReadOnlyList<string>>.Fail(
                    $"PANEL_CLADDING_SURFACE_COVERAGE_CELL_INVALID: {token}");
            }
            labels.Add(label);
        }
        return OperationResponse<IReadOnlyList<string>>.Ok(labels);
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();
}
