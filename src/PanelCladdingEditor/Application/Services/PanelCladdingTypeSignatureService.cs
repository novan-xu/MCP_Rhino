using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingTypeSignatureService
{
    private readonly PanelCladdingRegionService _regions;

    public PanelCladdingTypeSignatureService(PanelCladdingKeyService keys)
    {
        _regions = new PanelCladdingRegionService(keys);
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
        OperationResponse<PanelCladdingRegionSet> regionResponse = _regions.Resolve(
            orderedCells,
            requestedValues,
            requirePopulatedCells: true);
        if (!regionResponse.Success || regionResponse.Data is null)
        {
            return OperationResponse<PanelCladdingTypeIdentity>.Fail(regionResponse.Message);
        }
        IReadOnlyDictionary<string, string> normalized = regionResponse.Data.NormalizedCellValues;

        var payload = new StringBuilder();
        double unitScale = layout.ModelUnitScaleToMillimeters;
        payload.Append("v=3|size=")
            .Append(FormatNumber(layout.Width * unitScale))
            .Append(',')
            .Append(FormatNumber(layout.Height * unitScale))
            .Append("|h=");
        AppendOffsets(payload, layout.HorizontalOffsets.Select(value => value * unitScale));
        payload.Append("|v=");
        AppendOffsets(payload, layout.VerticalOffsets.Select(value => value * unitScale));
        payload.Append("|cells=");
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
            SchemaVersion = 3,
            TypeCode = typeCode,
            FullDigest = digest,
            StoredSignature = $"v3:sha256:{digest}",
            CanonicalPayload = canonical,
            NormalizedCellValues = normalized
        });
    }

    private static void AppendOffsets(StringBuilder payload, IEnumerable<double> values)
    {
        foreach (double value in values)
        {
            payload.Append(PanelCladdingKeyService.FormatOffset(value)).Append(';');
        }
    }

    private static string FormatNumber(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

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

