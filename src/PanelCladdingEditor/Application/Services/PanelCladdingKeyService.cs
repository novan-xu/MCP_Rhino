using System.Globalization;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingKeyService
{
    public const string TypeCodeKey = "CW_4.00_CLADDING_TYPE";
    public const string SignatureKey = "CW_4.00_CLADDING_SIGNATURE";
    public const string WorkbookPathDocumentKey = "PanelCladdingEditor.WorkbookPath";

    public OperationResponse<PanelCladdingKeySet> Parse(
        IReadOnlyDictionary<string, string> userText,
        double panelWidth,
        double panelHeight,
        double tolerance)
    {
        OperationResponse<IReadOnlyList<double>> horizontal = ParseOffsets(
            userText,
            HorizontalOffsetRegex(),
            "H",
            panelHeight,
            tolerance);
        if (!horizontal.Success || horizontal.Data is null)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(horizontal.Message);
        }

        OperationResponse<IReadOnlyList<double>> vertical = ParseOffsets(
            userText,
            VerticalOffsetRegex(),
            "V",
            panelWidth,
            tolerance);
        if (!vertical.Success || vertical.Data is null)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(vertical.Message);
        }

        int rowCount = horizontal.Data.Count + 1;
        int columnCount = vertical.Data.Count + 1;
        var cells = new List<PanelCladdingCell>(rowCount * columnCount);
        for (int column = 0; column < columnCount; column++)
        {
            for (int row = 0; row < rowCount; row++)
            {
                string rowLabel = GetRowLabel(row);
                string key = GetCellKey(column, rowLabel);
                userText.TryGetValue(key, out string? value);
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabel,
                    ShortLabel = $"{column}{rowLabel}",
                    UserTextKey = key,
                    Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()
                });
            }
        }

        return OperationResponse<PanelCladdingKeySet>.Ok(new PanelCladdingKeySet
        {
            HorizontalOffsets = horizontal.Data,
            VerticalOffsets = vertical.Data,
            Cells = cells
        });
    }

    public string NormalizeCladdingValue(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static string GetCellKey(int column, string rowLabel)
    {
        return $"CW_4.{column:00}_CLADDING_{column}{rowLabel}";
    }

    public static string GetRowLabel(int row)
    {
        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        string result = string.Empty;
        int value = row + 1;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }

        return result;
    }

    private static OperationResponse<IReadOnlyList<double>> ParseOffsets(
        IReadOnlyDictionary<string, string> userText,
        Regex regex,
        string label,
        double extent,
        double tolerance)
    {
        var byIndex = new SortedDictionary<int, double>();
        foreach ((string key, string rawValue) in userText)
        {
            Match match = regex.Match(key);
            if (!match.Success)
            {
                continue;
            }

            int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (byIndex.ContainsKey(index))
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_DUPLICATE_{label}_INDEX: duplicate {label}{index} key.");
            }

            if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !double.IsFinite(value))
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_INVALID_{label}_VALUE: {key} is not a finite invariant number.");
            }

            byIndex[index] = value;
        }

        int expected = 0;
        double previous = double.NegativeInfinity;
        var ordered = new List<double>(byIndex.Count);
        foreach ((int index, double value) in byIndex)
        {
            if (index != expected)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_GAPPED_{label}_INDICES: expected {label}{expected}, found {label}{index}.");
            }

            if (value <= tolerance || value >= extent - tolerance)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_{label}_OUTSIDE_EXTENT: {label}{index}={value.ToString("G17", CultureInfo.InvariantCulture)} is outside the panel interior.");
            }

            if (value <= previous + tolerance)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_NON_MONOTONIC_{label}: {label}{index} is not strictly greater than the prior offset.");
            }

            ordered.Add(value);
            previous = value;
            expected++;
        }

        return OperationResponse<IReadOnlyList<double>>.Ok(ordered);
    }

    [GeneratedRegex(@"^CW_2\.03_OFFSET_H(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalOffsetRegex();

    [GeneratedRegex(@"^CW_2\.04_OFFSET_V(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerticalOffsetRegex();
}

