using System.Security.Cryptography;
using System.Text;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelFrameTypologyService
{
    private const int SchemaVersion = 1;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelFrameAssignmentService _assignments;

    public PanelFrameTypologyService(
        PanelCladdingKeyService keys,
        PanelFrameAssignmentService? assignments = null)
    {
        _keys = keys;
        _assignments = assignments ?? new PanelFrameAssignmentService();
    }

    public OperationResponse<PanelFrameTypologyIdentity> Create(
        PanelCladdingLayout layout,
        PanelFrameAssignmentState assignments,
        string systemCode)
    {
        if (!layout.CanSave || layout.Width <= 0d || layout.Height <= 0d)
        {
            return OperationResponse<PanelFrameTypologyIdentity>.Fail(
                "PANEL_FRAME_TYPOLOGY_LAYOUT_INVALID");
        }
        OperationResponse<PanelFrameAssignmentState> normalized = _assignments.Normalize(
            assignments,
            layout.HorizontalOffsets.Count,
            layout.VerticalOffsets.Count,
            layout.Topology);
        if (!normalized.Success || normalized.Data is null)
        {
            return OperationResponse<PanelFrameTypologyIdentity>.Fail(normalized.Message);
        }
        if (normalized.Data.IsEmpty)
        {
            return OperationResponse<PanelFrameTypologyIdentity>.Fail(
                "PANEL_FRAME_TYPOLOGY_ASSIGNMENTS_REQUIRED");
        }

        OperationResponse<PanelCladdingTopologyPayloads> topology = _keys.EncodeTopology(
            layout.Topology,
            layout.HorizontalOffsets.Count,
            layout.VerticalOffsets.Count);
        if (!topology.Success || topology.Data is null)
        {
            return OperationResponse<PanelFrameTypologyIdentity>.Fail(topology.Message);
        }
        OperationResponse<string> encodedAssignments = _assignments.Encode(
            normalized.Data,
            layout.HorizontalOffsets.Count,
            layout.VerticalOffsets.Count,
            layout.Topology);
        if (!encodedAssignments.Success || encodedAssignments.Data is null)
        {
            return OperationResponse<PanelFrameTypologyIdentity>.Fail(encodedAssignments.Message);
        }

        string payload = new StringBuilder()
            .Append("frame-typology|v").Append(SchemaVersion)
            .Append("|width=").Append(PanelCladdingKeyService.FormatUnitDimension(layout.Width))
            .Append("|height=").Append(PanelCladdingKeyService.FormatUnitDimension(layout.Height))
            .Append("|h=").Append(string.Join(',', layout.HorizontalOffsets.Select(PanelCladdingKeyService.FormatOffset)))
            .Append("|v=").Append(string.Join(',', layout.VerticalOffsets.Select(PanelCladdingKeyService.FormatOffset)))
            .Append("|segment=").Append(topology.Data.SegmentMask)
            .Append("|merge=").Append(topology.Data.MergeMask)
            .Append("|hide=").Append(topology.Data.HideMask)
            .Append("|assignments=").Append(encodedAssignments.Data)
            .ToString();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        string prefix = NormalizeSystemCode(systemCode);
        string suffix = $"-{layout.ColumnCount}X{layout.RowCount}-{digest[..8].ToUpperInvariant()}";
        int maximumPrefixLength = Math.Max(1, 31 - suffix.Length);
        if (prefix.Length > maximumPrefixLength)
        {
            prefix = prefix[..maximumPrefixLength];
        }

        return OperationResponse<PanelFrameTypologyIdentity>.Ok(new PanelFrameTypologyIdentity
        {
            SchemaVersion = SchemaVersion,
            TypologyCode = prefix + suffix,
            FullDigest = digest,
            CanonicalPayload = payload
        });
    }

    private static string NormalizeSystemCode(string value)
    {
        string normalized = new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized)
            ? "PANEL"
            : normalized[..Math.Min(10, normalized.Length)];
    }
}
