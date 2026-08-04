using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingTypeSignatureService
{
    private readonly PanelCladdingKeyService _keys;

    public PanelCladdingTypeSignatureService(PanelCladdingKeyService keys)
    {
        _keys = keys;
    }

    public OperationResponse<PanelCladdingTypeIdentity> Create(
        PanelCladdingLayout layout,
        IReadOnlyDictionary<string, string> requestedValues,
        string requestedSystemCode)
    {
        if (!layout.CanSave)
        {
            return OperationResponse<PanelCladdingTypeIdentity>.Fail(
                $"PANEL_CLADDING_UNSUPPORTED_PROJECTION: {layout.GeometryDiagnostic}");
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in layout.Cells.OrderBy(item => item.Column).ThenBy(item => item.Row))
        {
            requestedValues.TryGetValue(cell.UserTextKey, out string? rawValue);
            string value = _keys.NormalizeCladdingValue(rawValue ?? cell.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return OperationResponse<PanelCladdingTypeIdentity>.Fail(
                    $"PANEL_CLADDING_CELL_REQUIRED: {cell.ShortLabel} ({cell.UserTextKey}) is blank.");
            }

            normalized[cell.UserTextKey] = value;
        }

        double scale = layout.ModelUnitScaleToMillimeters;
        double quantum = Math.Max(layout.ModelTolerance * scale, 0.01d);
        string geometryToken = layout.GeometryClass == PanelGeometryClass.Planar ? "P" : "C";
        var payload = new StringBuilder();
        payload.Append("v=1");
        payload.Append("|g=").Append(geometryToken);
        payload.Append("|w=").Append(Quantized(layout.Width * scale, quantum));
        payload.Append("|h=").Append(Quantized(layout.Height * scale, quantum));
        payload.Append("|rows=").Append(layout.RowCount.ToString(CultureInfo.InvariantCulture));
        payload.Append("|cols=").Append(layout.ColumnCount.ToString(CultureInfo.InvariantCulture));
        payload.Append("|H=").AppendJoin(',', layout.HorizontalOffsets.Select(value => Quantized(value * scale, quantum)));
        payload.Append("|V=").AppendJoin(',', layout.VerticalOffsets.Select(value => Quantized(value * scale, quantum)));
        payload.Append("|cells=");
        foreach (PanelCladdingCell cell in layout.Cells.OrderBy(item => item.Column).ThenBy(item => item.Row))
        {
            payload.Append(cell.ShortLabel)
                .Append(':')
                .Append(Escape(normalized[cell.UserTextKey]))
                .Append(';');
        }

        if (layout.GeometryClass == PanelGeometryClass.Curved)
        {
            payload.Append("|depth=")
                .AppendJoin(',', layout.Preview.DepthSamples.Select(value => Quantized(value * scale, quantum)));
        }

        string canonical = payload.ToString();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string systemCode = NormalizeSystemCode(string.IsNullOrWhiteSpace(requestedSystemCode)
            ? layout.SystemCode
            : requestedSystemCode);
        string typeCode = $"{systemCode}-{geometryToken}-{layout.ColumnCount}X{layout.RowCount}-{digest[..8].ToUpperInvariant()}";
        if (typeCode.Length > 31)
        {
            int over = typeCode.Length - 31;
            systemCode = systemCode[..Math.Max(2, systemCode.Length - over)];
            typeCode = $"{systemCode}-{geometryToken}-{layout.ColumnCount}X{layout.RowCount}-{digest[..8].ToUpperInvariant()}";
        }

        return OperationResponse<PanelCladdingTypeIdentity>.Ok(new PanelCladdingTypeIdentity
        {
            SchemaVersion = 1,
            TypeCode = typeCode,
            FullDigest = digest,
            StoredSignature = $"v1:sha256:{digest}",
            CanonicalPayload = canonical,
            NormalizedCellValues = normalized
        });
    }

    public static string WithDigestLength(PanelCladdingTypeIdentity identity, int digestLength)
    {
        string[] parts = identity.TypeCode.Split('-');
        if (parts.Length < 4)
        {
            throw new InvalidOperationException("Type code does not contain the expected digest segment.");
        }

        digestLength = Math.Clamp(digestLength, 8, Math.Min(20, identity.FullDigest.Length));
        parts[^1] = identity.FullDigest[..digestLength].ToUpperInvariant();
        string candidate = string.Join('-', parts);
        return candidate.Length <= 31 ? candidate : candidate[^31..];
    }

    private static string Quantized(double value, double quantum)
    {
        double quantized = Math.Round(value / quantum, MidpointRounding.AwayFromZero) * quantum;
        return quantized.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string NormalizeSystemCode(string value)
    {
        string normalized = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "PANEL";
        }

        return normalized.Length > 10 ? normalized[..10] : normalized;
    }
}

