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

        PanelCladdingCell[] orderedCells = layout.Cells
            .OrderBy(item => item.Column)
            .ThenBy(item => item.Row)
            .ToArray();
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in orderedCells)
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

        var payload = new StringBuilder();
        payload.Append("v=2|cells=");
        foreach (PanelCladdingCell cell in orderedCells)
        {
            payload.Append(cell.ShortLabel)
                .Append(':')
                .Append(Escape(normalized[cell.UserTextKey]))
                .Append(';');
        }

        string canonical = payload.ToString();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        string systemCode = NormalizeSystemCode(string.IsNullOrWhiteSpace(requestedSystemCode)
            ? layout.SystemCode
            : requestedSystemCode);
        int columnCount = orderedCells.Select(cell => cell.Column).Distinct().Count();
        int rowCount = orderedCells.Select(cell => cell.Row).Distinct().Count();
        string typeCode = $"{systemCode}-CL-{columnCount}X{rowCount}-{digest[..8].ToUpperInvariant()}";
        if (typeCode.Length > 31)
        {
            int over = typeCode.Length - 31;
            systemCode = systemCode[..Math.Max(2, systemCode.Length - over)];
            typeCode = $"{systemCode}-CL-{columnCount}X{rowCount}-{digest[..8].ToUpperInvariant()}";
        }

        return OperationResponse<PanelCladdingTypeIdentity>.Ok(new PanelCladdingTypeIdentity
        {
            SchemaVersion = 2,
            TypeCode = typeCode,
            FullDigest = digest,
            StoredSignature = $"v2:sha256:{digest}",
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

