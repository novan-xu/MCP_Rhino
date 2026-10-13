using System.Text.Json;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingKeyService
{
    public OperationResponse<string> EncodeFrameConfiguration(
        PanelCladdingTopologyState topology, int horizontalTrackCount, int verticalTrackCount)
    {
        OperationResponse<PanelCladdingTopologyPayloads> masks = EncodeTopology(
            topology, horizontalTrackCount, verticalTrackCount);
        return !masks.Success || masks.Data is null
            ? OperationResponse<string>.Fail(masks.Message)
            : OperationResponse<string>.Ok(JsonSerializer.Serialize(new
            {
                v = 1,
                delete = masks.Data.SegmentMask,
                merge = masks.Data.MergeMask,
                hide = masks.Data.HideMask
            }));
    }

    public OperationResponse<IReadOnlyDictionary<string, string>> EncodeFrameConfigurationUserText(
        PanelCladdingTopologyState topology, int horizontalTrackCount, int verticalTrackCount)
    {
        OperationResponse<string> encoded = EncodeFrameConfiguration(topology, horizontalTrackCount, verticalTrackCount);
        return !encoded.Success || encoded.Data is null
            ? OperationResponse<IReadOnlyDictionary<string, string>>.Fail(encoded.Message)
            : OperationResponse<IReadOnlyDictionary<string, string>>.Ok(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [FrameConfigKey] = encoded.Data });
    }

    public static bool IsRetiredFrameKey(string key) =>
        string.Equals(key, LegacyFrameTypologyKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, LegacyFrameAssignmentsKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, SegmentMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, MergeMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, HideMaskKey, StringComparison.OrdinalIgnoreCase);

    internal static OperationResponse<string> ReadFrameAttribute(
        IReadOnlyDictionary<string, string> userText, string key)
    {
        string[] values = userText.Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Value ?? string.Empty).Distinct(StringComparer.Ordinal).ToArray();
        return values.Length > 1
            ? OperationResponse<string>.Fail($"PANEL_FRAME_ATTRIBUTE_CONFLICT: {key}")
            : OperationResponse<string>.Ok(values.FirstOrDefault() ?? string.Empty);
    }

    public static OperationResponse ValidateFrameAttributeNames(IReadOnlyDictionary<string, string> userText)
    {
        foreach ((string current, string[] legacy) in new[]
                 {
                     (FrameConfigKey, new[] { SegmentMaskKey, MergeMaskKey, HideMaskKey }),
                     (FrameTypeKey, new[] { LegacyFrameAssignmentsKey })
                 })
        {
            OperationResponse<string> value = ReadFrameAttribute(userText, current);
            if (!value.Success) return OperationResponse.Fail(value.Message);
            if (!string.IsNullOrWhiteSpace(value.Data)) continue;
            foreach (string key in legacy)
            {
                value = ReadFrameAttribute(userText, key);
                if (!value.Success) return OperationResponse.Fail(value.Message);
            }
        }
        return OperationResponse.Ok();
    }

    private static OperationResponse<PanelCladdingTopologyPayloads> ReadFrameConfiguration(
        IReadOnlyDictionary<string, string> userText)
    {
        OperationResponse<string> config = ReadFrameAttribute(userText, FrameConfigKey);
        if (!config.Success)
        {
            return OperationResponse<PanelCladdingTopologyPayloads>.Fail(config.Message);
        }
        if (!string.IsNullOrWhiteSpace(config.Data))
        {
            if (config.Data.Length > 8_000_000)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail("PANEL_FRAME_CONFIG_PAYLOAD_TOO_LARGE");
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(config.Data);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    root.EnumerateObject().Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != 4 ||
                    root.EnumerateObject().Count() != 4 || root.GetProperty("v").GetInt32() != 1)
                {
                    throw new FormatException("Expected version 1 and delete, merge, hide masks.");
                }
                string Mask(string name)
                {
                    string? value = root.GetProperty(name).GetString();
                    return !string.IsNullOrWhiteSpace(value)
                        ? value : throw new FormatException($"Missing {name} mask.");
                }
                return OperationResponse<PanelCladdingTopologyPayloads>.Ok(new()
                {
                    SegmentMask = Mask("delete"), MergeMask = Mask("merge"), HideMask = Mask("hide")
                });
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or
                                                KeyNotFoundException or FormatException or OverflowException)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail($"PANEL_FRAME_CONFIG_INVALID: {error.Message}");
            }
        }

        OperationResponse<string> delete = ReadFrameAttribute(userText, SegmentMaskKey);
        OperationResponse<string> merge = ReadFrameAttribute(userText, MergeMaskKey);
        OperationResponse<string> hide = ReadFrameAttribute(userText, HideMaskKey);
        foreach (OperationResponse<string> mask in new[] { delete, merge, hide })
        {
            if (!mask.Success) return OperationResponse<PanelCladdingTopologyPayloads>.Fail(mask.Message);
        }
        return OperationResponse<PanelCladdingTopologyPayloads>.Ok(new()
        {
            SegmentMask = delete.Data ?? string.Empty,
            MergeMask = merge.Data ?? string.Empty,
            HideMask = hide.Data ?? string.Empty
        });
    }
}
