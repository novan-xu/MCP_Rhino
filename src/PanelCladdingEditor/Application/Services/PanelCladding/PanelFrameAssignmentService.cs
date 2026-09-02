using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelFrameAssignmentService
{
    private const int PayloadVersion = 2;
    private const int MaximumPayloadLength = 1_000_000;
    private const int MaximumAssignmentCount = 100_000;

    public OperationResponse<PanelFrameAssignmentState> Decode(
        IReadOnlyDictionary<string, string> userText,
        int horizontalTrackCount,
        int verticalTrackCount,
        PanelCladdingTopologyState topology)
    {
        string payload = userText.FirstOrDefault(item => string.Equals(
            item.Key,
            PanelCladdingKeyService.FrameAssignmentsKey,
            StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        return Decode(payload, horizontalTrackCount, verticalTrackCount, topology);
    }

    public OperationResponse<PanelFrameAssignmentState> Decode(
        string? payload,
        int horizontalTrackCount,
        int verticalTrackCount,
        PanelCladdingTopologyState topology)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return OperationResponse<PanelFrameAssignmentState>.Ok(new PanelFrameAssignmentState());
        }
        if (payload.Length > MaximumPayloadLength)
        {
            return OperationResponse<PanelFrameAssignmentState>.Fail(
                "PANEL_FRAME_ASSIGNMENTS_PAYLOAD_TOO_LARGE");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("v", out JsonElement versionElement))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    "PANEL_FRAME_ASSIGNMENTS_VERSION_UNSUPPORTED");
            }
            int version = versionElement.GetInt32();
            if (version is not 1 and not PayloadVersion)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    "PANEL_FRAME_ASSIGNMENTS_VERSION_UNSUPPORTED");
            }

            var frames = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("f", out JsonElement frameElement))
            {
                if (frameElement.ValueKind != JsonValueKind.Object)
                {
                    return OperationResponse<PanelFrameAssignmentState>.Fail(
                        "PANEL_FRAME_ASSIGNMENTS_FRAMES_INVALID");
                }
                foreach (JsonProperty property in frameElement.EnumerateObject())
                {
                    frames[property.Name] = ReadCodes(property.Value);
                }
            }

            var segments = new List<PanelFrameSegmentAssignment>();
            if (root.TryGetProperty("s", out JsonElement segmentElement))
            {
                if (segmentElement.ValueKind != JsonValueKind.Array)
                {
                    return OperationResponse<PanelFrameAssignmentState>.Fail(
                        "PANEL_FRAME_ASSIGNMENTS_SEGMENTS_INVALID");
                }
                foreach (JsonElement item in segmentElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("a", out JsonElement axisElement) ||
                        !item.TryGetProperty("t", out JsonElement trackElement) ||
                        !item.TryGetProperty("b", out JsonElement bayElement) ||
                        !item.TryGetProperty("c", out JsonElement codesElement))
                    {
                        return OperationResponse<PanelFrameAssignmentState>.Fail(
                            "PANEL_FRAME_ASSIGNMENTS_SEGMENT_INVALID");
                    }
                    PanelCladdingTopologyAxis axis = (axisElement.GetString() ?? string.Empty) switch
                    {
                        "H" => PanelCladdingTopologyAxis.Horizontal,
                        "V" => PanelCladdingTopologyAxis.Vertical,
                        _ => throw new FormatException("Unknown frame-assignment axis.")
                    };
                    segments.Add(new PanelFrameSegmentAssignment(
                        new PanelCladdingSegmentCoordinate(axis, trackElement.GetInt32(), bayElement.GetInt32()),
                        ReadCodes(codesElement)));
                }
            }

            var definitions = new Dictionary<string, PanelFrameProfileDefinition>(StringComparer.OrdinalIgnoreCase);
            if (version == PayloadVersion && root.TryGetProperty("d", out JsonElement definitionElement))
            {
                if (definitionElement.ValueKind != JsonValueKind.Object)
                {
                    return OperationResponse<PanelFrameAssignmentState>.Fail(
                        "PANEL_FRAME_ASSIGNMENTS_DEFINITIONS_INVALID");
                }
                foreach (JsonProperty property in definitionElement.EnumerateObject())
                {
                    definitions[property.Name] = ReadDefinition(property.Name, property.Value);
                }
            }

            var modifiers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (version == PayloadVersion && root.TryGetProperty("x", out JsonElement modifierElement))
            {
                if (modifierElement.ValueKind != JsonValueKind.Object)
                {
                    return OperationResponse<PanelFrameAssignmentState>.Fail(
                        "PANEL_FRAME_ASSIGNMENTS_MODIFIERS_INVALID");
                }
                foreach (JsonProperty property in modifierElement.EnumerateObject())
                {
                    modifiers[property.Name] = property.Value.GetDouble();
                }
            }

            return Normalize(new PanelFrameAssignmentState
            {
                FrameAssignments = frames,
                SegmentAssignments = segments,
                Definitions = definitions,
                CurveModifiers = modifiers
            }, horizontalTrackCount, verticalTrackCount, topology);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            return OperationResponse<PanelFrameAssignmentState>.Fail(
                $"PANEL_FRAME_ASSIGNMENTS_PAYLOAD_INVALID: {exception.Message}");
        }
    }

    public OperationResponse<PanelFrameAssignmentState> Normalize(
        PanelFrameAssignmentState? state,
        int horizontalTrackCount,
        int verticalTrackCount,
        PanelCladdingTopologyState topology)
    {
        if (horizontalTrackCount < 0 || verticalTrackCount < 0)
        {
            return OperationResponse<PanelFrameAssignmentState>.Fail(
                "PANEL_FRAME_ASSIGNMENTS_TRACK_COUNT_INVALID");
        }

        var frames = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach ((string rawFrame, IReadOnlyList<string> rawCodes) in state?.FrameAssignments ??
                 new Dictionary<string, IReadOnlyList<string>>())
        {
            string frame = rawFrame.Trim().ToUpperInvariant();
            if (!FrameKeyRegex().IsMatch(frame))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_FRAME_INVALID: {rawFrame}");
            }
            OperationResponse<IReadOnlyList<string>> codes = NormalizeCodes(rawCodes);
            if (!codes.Success || codes.Data is null)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(codes.Message);
            }
            if (codes.Data.Count > 0)
            {
                frames[frame] = codes.Data;
            }
        }

        var missing = topology.MissingSegments.ToHashSet();
        var segmentMap = new Dictionary<PanelCladdingSegmentCoordinate, IReadOnlyList<string>>();
        foreach (PanelFrameSegmentAssignment assignment in state?.SegmentAssignments ??
                 Array.Empty<PanelFrameSegmentAssignment>())
        {
            PanelCladdingSegmentCoordinate segment = assignment.Segment;
            int trackCount = segment.Axis == PanelCladdingTopologyAxis.Horizontal
                ? horizontalTrackCount
                : verticalTrackCount;
            int bayCount = segment.Axis == PanelCladdingTopologyAxis.Horizontal
                ? verticalTrackCount + 1
                : horizontalTrackCount + 1;
            if (segment.Track < 0 || segment.Track >= trackCount || segment.Bay < 0 || segment.Bay >= bayCount)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_SEGMENT_OUT_OF_RANGE: {SegmentKey(segment)}");
            }
            if (missing.Contains(segment))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_SEGMENT_MISSING: {SegmentKey(segment)}");
            }
            OperationResponse<IReadOnlyList<string>> codes = NormalizeCodes(assignment.Codes);
            if (!codes.Success || codes.Data is null)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(codes.Message);
            }
            if (codes.Data.Count == 0)
            {
                continue;
            }
            if (!segmentMap.TryAdd(segment, codes.Data))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_SEGMENT_DUPLICATE: {SegmentKey(segment)}");
            }
        }

        if (frames.Count + segmentMap.Count > MaximumAssignmentCount)
        {
            return OperationResponse<PanelFrameAssignmentState>.Fail("PANEL_FRAME_ASSIGNMENTS_COUNT_EXCEEDED");
        }

        var usedCodes = frames.Values.Concat(segmentMap.Values)
            .SelectMany(codes => codes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        var sourceDefinitions = state?.Definitions ??
            new Dictionary<string, PanelFrameProfileDefinition>(StringComparer.OrdinalIgnoreCase);
        var definitions = new SortedDictionary<string, PanelFrameProfileDefinition>(StringComparer.Ordinal);
        foreach (string code in usedCodes)
        {
            if (!sourceDefinitions.TryGetValue(code, out PanelFrameProfileDefinition? definition) &&
                code.StartsWith("0D-", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_DEFINITION_MISSING: {code}");
            }
            definition ??= CreateLegacyDefinition(code);
            OperationResponse<PanelFrameProfileDefinition> normalizedDefinition = NormalizeDefinition(code, definition);
            if (!normalizedDefinition.Success || normalizedDefinition.Data is null)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(normalizedDefinition.Message);
            }
            definitions[code] = normalizedDefinition.Data;
        }

        var assignedKeys = frames.Keys.Concat(segmentMap.Keys.Select(SegmentKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modifiers = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach ((string rawKey, double value) in state?.CurveModifiers ??
                 new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase))
        {
            string key = rawKey.Trim().ToUpperInvariant();
            if (!assignedKeys.Contains(key))
            {
                continue;
            }
            if (!double.IsFinite(value) || Math.Abs(value) > 100_000d)
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_MODIFIER_INVALID: {rawKey}");
            }
            if (Math.Abs(value) > 1e-9d)
            {
                modifiers[key] = value;
            }
        }

        foreach (PanelCladdingMergeRun run in topology.MergeRuns)
        {
            PanelCladdingSegmentCoordinate[] atoms = Enumerable
                .Range(run.StartBay, run.EndBay - run.StartBay + 1)
                .Select(bay => new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay))
                .ToArray();
            IReadOnlyList<string>[] sets = atoms
                .Select(segment => segmentMap.GetValueOrDefault(segment, Array.Empty<string>()))
                .ToArray();
            if (sets.Skip(1).Any(set => !sets[0].SequenceEqual(set, StringComparer.Ordinal)))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_MERGED_CURVE_INCONSISTENT: " +
                    $"{run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}");
            }
            double firstModifier = modifiers.GetValueOrDefault(SegmentKey(atoms[0]));
            if (atoms.Skip(1).Any(atom =>
                    Math.Abs(modifiers.GetValueOrDefault(SegmentKey(atom)) - firstModifier) > 1e-9d))
            {
                return OperationResponse<PanelFrameAssignmentState>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_MERGED_MODIFIER_INCONSISTENT: " +
                    $"{run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}");
            }
        }

        return OperationResponse<PanelFrameAssignmentState>.Ok(new PanelFrameAssignmentState
        {
            FrameAssignments = frames,
            SegmentAssignments = segmentMap
                .OrderBy(item => item.Key.Axis)
                .ThenBy(item => item.Key.Track)
                .ThenBy(item => item.Key.Bay)
                .Select(item => new PanelFrameSegmentAssignment(item.Key, item.Value))
                .ToArray(),
            Definitions = definitions,
            CurveModifiers = modifiers
        });
    }

    public OperationResponse<string> Encode(
        PanelFrameAssignmentState? state,
        int horizontalTrackCount,
        int verticalTrackCount,
        PanelCladdingTopologyState topology)
    {
        OperationResponse<PanelFrameAssignmentState> normalized = Normalize(
            state, horizontalTrackCount, verticalTrackCount, topology);
        if (!normalized.Success || normalized.Data is null)
        {
            return OperationResponse<string>.Fail(normalized.Message);
        }
        if (normalized.Data.IsEmpty)
        {
            return OperationResponse<string>.Ok(string.Empty);
        }

        PanelFrameAssignmentState value = normalized.Data;
        var payload = new StringBuilder();
        payload.Append("{\"v\":2,\"f\":{");
        bool first = true;
        foreach ((string frame, IReadOnlyList<string> codes) in value.FrameAssignments)
        {
            AppendSeparator(payload, ref first);
            payload.Append(JsonSerializer.Serialize(frame)).Append(':').Append(SerializeCodes(codes));
        }
        payload.Append("},\"s\":[");
        first = true;
        foreach (PanelFrameSegmentAssignment assignment in value.SegmentAssignments)
        {
            AppendSeparator(payload, ref first);
            payload.Append("{\"a\":\"")
                .Append(assignment.Segment.Axis == PanelCladdingTopologyAxis.Horizontal ? 'H' : 'V')
                .Append("\",\"t\":").Append(assignment.Segment.Track.ToString(CultureInfo.InvariantCulture))
                .Append(",\"b\":").Append(assignment.Segment.Bay.ToString(CultureInfo.InvariantCulture))
                .Append(",\"c\":").Append(SerializeCodes(assignment.Codes)).Append('}');
        }
        payload.Append("],\"d\":{");
        first = true;
        foreach ((string code, PanelFrameProfileDefinition definition) in value.Definitions)
        {
            AppendSeparator(payload, ref first);
            payload.Append(JsonSerializer.Serialize(code)).Append(':').Append(SerializeDefinition(definition));
        }
        payload.Append("},\"x\":{");
        first = true;
        foreach ((string key, double modifier) in value.CurveModifiers)
        {
            AppendSeparator(payload, ref first);
            payload.Append(JsonSerializer.Serialize(key)).Append(':')
                .Append(modifier.ToString("0.#####", CultureInfo.InvariantCulture));
        }
        payload.Append("}}");
        return OperationResponse<string>.Ok(payload.ToString());
    }

    public static IReadOnlyList<string> CodesForCurve(
        PanelFrameAssignmentState state,
        string curveCode,
        IReadOnlyList<PanelCladdingSegmentCoordinate> atomicSegments)
    {
        if (atomicSegments.Count == 0)
        {
            return state.FrameAssignments.TryGetValue(curveCode, out IReadOnlyList<string>? frameCodes)
                ? frameCodes
                : Array.Empty<string>();
        }
        var map = state.SegmentAssignments.ToDictionary(item => item.Segment, item => item.Codes);
        return atomicSegments
            .SelectMany(segment => map.GetValueOrDefault(segment, Array.Empty<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }

    public static double ModifierForCurve(
        PanelFrameAssignmentState state,
        string curveCode,
        IReadOnlyList<PanelCladdingSegmentCoordinate> atomicSegments)
    {
        string key = atomicSegments.Count == 0 ? curveCode : SegmentKey(atomicSegments[0]);
        return state.CurveModifiers.GetValueOrDefault(key);
    }

    public static bool IsExtrusionCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ExtrusionCodeRegex().IsMatch(code.Trim().ToUpperInvariant());

    public static string SegmentKey(PanelCladdingSegmentCoordinate segment) =>
        $"{(segment.Axis == PanelCladdingTopologyAxis.Horizontal ? 'H' : 'V')}:" +
        $"{segment.Track.ToString(CultureInfo.InvariantCulture)}:" +
        segment.Bay.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseSegmentKey(string key, out PanelCladdingSegmentCoordinate segment)
    {
        segment = default;
        string[] parts = (key ?? string.Empty).Split(':');
        if (parts.Length != 3 ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int track) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bay))
        {
            return false;
        }
        PanelCladdingTopologyAxis axis;
        if (string.Equals(parts[0], "H", StringComparison.Ordinal))
        {
            axis = PanelCladdingTopologyAxis.Horizontal;
        }
        else if (string.Equals(parts[0], "V", StringComparison.Ordinal))
        {
            axis = PanelCladdingTopologyAxis.Vertical;
        }
        else
        {
            return false;
        }
        segment = new PanelCladdingSegmentCoordinate(axis, track, bay);
        return track >= 0 && bay >= 0;
    }

    public static OperationResponse<IReadOnlyList<string>> NormalizeCodes(IEnumerable<string>? codes)
    {
        string[] normalized = (codes ?? Array.Empty<string>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        string? invalid = normalized.FirstOrDefault(code => !ExtrusionCodeRegex().IsMatch(code));
        return invalid is null
            ? OperationResponse<IReadOnlyList<string>>.Ok(normalized)
            : OperationResponse<IReadOnlyList<string>>.Fail(
                $"PANEL_FRAME_ASSIGNMENTS_CODE_INVALID: {invalid}");
    }

    private static PanelFrameProfileDefinition ReadDefinition(string code, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Frame-profile definition must be an object.");
        }
        string mode = element.TryGetProperty("m", out JsonElement modeElement)
            ? modeElement.GetString() ?? string.Empty
            : string.Empty;
        PanelFrameProfileCalculation calculation = mode switch
        {
            "L" => PanelFrameProfileCalculation.Length,
            "F" => PanelFrameProfileCalculation.FixedQuantity,
            "S" => PanelFrameProfileCalculation.Spacing,
            _ => throw new FormatException("Unknown frame-profile calculation mode.")
        };
        return new PanelFrameProfileDefinition
        {
            Code = code,
            BaseCode = ReadOptionalString(element, "b"),
            SourceCode = ReadOptionalString(element, "s"),
            Category = ReadOptionalString(element, "g"),
            Dimension = code.StartsWith("0D-", StringComparison.OrdinalIgnoreCase)
                ? PanelFrameProfileDimension.ZeroDimensional
                : PanelFrameProfileDimension.OneDimensional,
            Calculation = calculation,
            CalculationValue = element.TryGetProperty("n", out JsonElement numberElement)
                ? numberElement.GetDouble()
                : null,
            ParentCode = ReadOptionalString(element, "p")
        };
    }

    private static OperationResponse<PanelFrameProfileDefinition> NormalizeDefinition(
        string assignedCode,
        PanelFrameProfileDefinition definition)
    {
        string code = assignedCode.Trim().ToUpperInvariant();
        string baseCode = string.IsNullOrWhiteSpace(definition.BaseCode)
            ? BaseCodeFromReadyCode(code)
            : definition.BaseCode.Trim().ToUpperInvariant();
        string sourceCode = string.IsNullOrWhiteSpace(definition.SourceCode)
            ? baseCode
            : definition.SourceCode.Trim().ToUpperInvariant();
        string parent = definition.ParentCode.Trim().ToUpperInvariant();
        PanelFrameProfileDimension dimension = code.StartsWith("0D-", StringComparison.Ordinal)
            ? PanelFrameProfileDimension.ZeroDimensional
            : PanelFrameProfileDimension.OneDimensional;
        if (definition.Dimension != dimension ||
            (dimension == PanelFrameProfileDimension.OneDimensional &&
             definition.Calculation != PanelFrameProfileCalculation.Length) ||
            (dimension == PanelFrameProfileDimension.ZeroDimensional &&
             definition.Calculation == PanelFrameProfileCalculation.Length))
        {
            return OperationResponse<PanelFrameProfileDefinition>.Fail(
                $"PANEL_FRAME_ASSIGNMENTS_DEFINITION_MODE_INVALID: {code}");
        }
        if (!string.IsNullOrEmpty(parent) && (!ExtrusionCodeRegex().IsMatch(parent) ||
                                              string.Equals(parent, code, StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResponse<PanelFrameProfileDefinition>.Fail(
                $"PANEL_FRAME_ASSIGNMENTS_PARENT_INVALID: {code}");
        }
        double? number = definition.CalculationValue;
        if (dimension == PanelFrameProfileDimension.OneDimensional)
        {
            number ??= 1d;
            if (!IsPositiveInteger(number.Value))
            {
                return OperationResponse<PanelFrameProfileDefinition>.Fail(
                    $"PANEL_FRAME_ASSIGNMENTS_QUANTITY_INVALID: {code}");
            }
        }
        else if (number is null || !double.IsFinite(number.Value) || number.Value <= 0d ||
                 (definition.Calculation == PanelFrameProfileCalculation.FixedQuantity &&
                  !IsPositiveInteger(number.Value)))
        {
            return OperationResponse<PanelFrameProfileDefinition>.Fail(
                $"PANEL_FRAME_ASSIGNMENTS_VALUE_INVALID: {code}");
        }
        return OperationResponse<PanelFrameProfileDefinition>.Ok(new PanelFrameProfileDefinition
        {
            Code = code,
            BaseCode = baseCode,
            SourceCode = sourceCode,
            Category = definition.Category.Trim().ToUpperInvariant(),
            Dimension = dimension,
            Calculation = definition.Calculation,
            CalculationValue = number,
            ParentCode = parent
        });
    }

    private static PanelFrameProfileDefinition CreateLegacyDefinition(string code)
    {
        if (code.StartsWith("0D-", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"A 0D profile requires a stored calculation mode: {code}");
        }
        return new PanelFrameProfileDefinition
        {
            Code = code,
            BaseCode = BaseCodeFromReadyCode(code),
            SourceCode = BaseCodeFromReadyCode(code),
            Dimension = PanelFrameProfileDimension.OneDimensional,
            Calculation = PanelFrameProfileCalculation.Length,
            CalculationValue = 1d
        };
    }

    private static string BaseCodeFromReadyCode(string code)
    {
        string value = code.Length > 3 ? code[3..] : code;
        return value.StartsWith("ALU-", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
    }

    private static bool IsPositiveInteger(double value) =>
        double.IsFinite(value) && value >= 1d && value <= 100_000d && Math.Abs(value - Math.Round(value)) < 1e-9d;

    private static IReadOnlyList<string> ReadCodes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Frame-assignment code set must be an array.");
        }
        return element.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static string ReadOptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string SerializeCodes(IReadOnlyList<string> codes) => JsonSerializer.Serialize(codes);

    private static string SerializeDefinition(PanelFrameProfileDefinition definition)
    {
        string mode = definition.Calculation switch
        {
            PanelFrameProfileCalculation.Length => "L",
            PanelFrameProfileCalculation.FixedQuantity => "F",
            PanelFrameProfileCalculation.Spacing => "S",
            _ => throw new InvalidOperationException("Unknown frame-profile calculation mode.")
        };
        var payload = new StringBuilder("{");
        payload.Append("\"b\":").Append(JsonSerializer.Serialize(definition.BaseCode))
            .Append(",\"s\":").Append(JsonSerializer.Serialize(definition.SourceCode))
            .Append(",\"g\":").Append(JsonSerializer.Serialize(definition.Category))
            .Append(",\"m\":").Append(JsonSerializer.Serialize(mode));
        if (definition.CalculationValue is double number)
        {
            payload.Append(",\"n\":").Append(number.ToString("0.#####", CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrWhiteSpace(definition.ParentCode))
        {
            payload.Append(",\"p\":").Append(JsonSerializer.Serialize(definition.ParentCode));
        }
        return payload.Append('}').ToString();
    }

    private static void AppendSeparator(StringBuilder builder, ref bool first)
    {
        if (!first)
        {
            builder.Append(',');
        }
        first = false;
    }

    [GeneratedRegex(@"^FRM_[0-3]$", RegexOptions.CultureInvariant)]
    private static partial Regex FrameKeyRegex();

    [GeneratedRegex(@"^(?:0D|1D)-[A-Z0-9][A-Z0-9-]{0,76}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtrusionCodeRegex();
}
