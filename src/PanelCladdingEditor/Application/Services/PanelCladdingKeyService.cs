using System.Globalization;
using System.Buffers.Binary;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed partial class PanelCladdingKeyService
{
    public const int OffsetDecimalPlaces = 5;
    public const string TypeCodeKey = "CW_1.10_CLADDING_TYPE";
    public const string FrameTypologyKey = "CW_1.5D_FRAME TYPOLOGY";
    public const string LegacyTypeCodeKey = "CW_4.00_CLADDING_TYPE";
    public const string SignatureKey = "Signature";
    public const string LegacySignatureKey = "CW_4.00_CLADDING_SIGNATURE";
    public const string SegmentMaskKey = "CW_2.05_SEGMENT_MASK";
    public const string MergeMaskKey = "CW_2.06_MERGE_MASK";
    public const string HideMaskKey = "CW_2.07_HIDE_MASK";
    public const string CladdingLogicKey = "CW_2.08_CLADDING_LOGIC";
    public const string FrameAssignmentsKey = "CW_2.09_FRAME_ASSIGNMENTS";
    public const string UnitDimensionKey = "CW_2.00_UNIT_DIMENSION";
    public const string UnitWidthKey = "CW_2.01_UNIT_WIDTH";
    public const string UnitHeightKey = "CW_2.02_UNIT_HEIGHT";
    public const string WorkbookPathDocumentKey = "PanelCladdingEditor.WorkbookPath";
    public const string PersistedBlankCellValue = " ";
    private const byte TopologyPayloadVersion = 1;
    private const byte SegmentPayloadKind = 1;
    private const byte MergePayloadKind = 2;
    private const byte HidePayloadKind = 3;
    private const int TopologyHeaderLength = 10;
    private const int MaxTopologyBitCount = 16_000_000;

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

        OperationResponse<PanelCladdingKeySet> keySet = CreateKeySet(
            horizontal.Data,
            vertical.Data,
            userText,
            panelWidth,
            panelHeight,
            tolerance);
        if (!keySet.Success || keySet.Data is null)
        {
            return keySet;
        }
        OperationResponse<PanelCladdingTopologyState> topology = DecodeTopology(
            userText,
            horizontal.Data.Count,
            vertical.Data.Count);
        if (!topology.Success || topology.Data is null)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(topology.Message);
        }
        OperationResponse<PanelFrameAssignmentState> frameAssignments =
            new PanelFrameAssignmentService().Decode(
                userText,
                horizontal.Data.Count,
                vertical.Data.Count,
                topology.Data);
        if (!frameAssignments.Success || frameAssignments.Data is null)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(frameAssignments.Message);
        }
        return OperationResponse<PanelCladdingKeySet>.Ok(new PanelCladdingKeySet
        {
            HorizontalOffsets = keySet.Data.HorizontalOffsets,
            VerticalOffsets = keySet.Data.VerticalOffsets,
            Cells = keySet.Data.Cells,
            Topology = topology.Data,
            FrameAssignments = frameAssignments.Data
        });
    }

    public OperationResponse<PanelCladdingKeySet> CreateKeySet(
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        IReadOnlyDictionary<string, string> cellValues,
        double panelWidth,
        double panelHeight,
        double tolerance)
    {
        OperationResponse<IReadOnlyList<double>> horizontal = ValidateOffsets(
            horizontalOffsets,
            "H",
            panelHeight,
            tolerance);
        if (!horizontal.Success || horizontal.Data is null)
        {
            return OperationResponse<PanelCladdingKeySet>.Fail(horizontal.Message);
        }
        OperationResponse<IReadOnlyList<double>> vertical = ValidateOffsets(
            verticalOffsets,
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
                cellValues.TryGetValue(key, out string? value);
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
            Cells = cells,
            Topology = new PanelCladdingTopologyState()
        });
    }

    public string NormalizeCladdingValue(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static string EncodeCellValueForStorage(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? PersistedBlankCellValue : value;
    }

    public static bool IsCellLabelToken(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && CellLabelRegex().IsMatch(value.Trim());
    }

    public bool IsClearableCladdingAssignmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return CladdingCellRegex().IsMatch(key) ||
            string.Equals(key, TypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, LegacyTypeCodeKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, SignatureKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, LegacySignatureKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, CladdingLogicKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, FrameAssignmentsKey, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, FrameTypologyKey, StringComparison.OrdinalIgnoreCase) ||
            IsTopologyKey(key);
    }

    public bool IsCladdingCellKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && CladdingCellRegex().IsMatch(key);
    }

    public bool IsOffsetKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
            (HorizontalOffsetRegex().IsMatch(key) || VerticalOffsetRegex().IsMatch(key));
    }

    public static bool IsTopologyKey(string key) =>
        string.Equals(key, SegmentMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, MergeMaskKey, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, HideMaskKey, StringComparison.OrdinalIgnoreCase);

    public static bool HasNonblankMergeMask(IReadOnlyDictionary<string, string> userText) =>
        HasNonblankValue(userText, MergeMaskKey);

    public OperationResponse<PanelCladdingTopologyPayloads> EncodeTopology(
        PanelCladdingTopologyState topology,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        OperationResponse dimensions = ValidateTopologyDimensions(horizontalTrackCount, verticalTrackCount);
        if (!dimensions.Success)
        {
            return OperationResponse<PanelCladdingTopologyPayloads>.Fail(dimensions.Message);
        }

        int columnCount = verticalTrackCount + 1;
        int rowCount = horizontalTrackCount + 1;
        int segmentBitCount = horizontalTrackCount * columnCount + verticalTrackCount * rowCount;
        int mergeBitCount = horizontalTrackCount * Math.Max(0, columnCount - 1) +
            verticalTrackCount * Math.Max(0, rowCount - 1);
        var segmentBits = Enumerable.Repeat(true, segmentBitCount).ToArray();
        var mergeBits = new bool[mergeBitCount];
        var hideBits = new bool[segmentBitCount];
        var missing = new HashSet<PanelCladdingSegmentCoordinate>();
        var hidden = new HashSet<PanelCladdingSegmentCoordinate>();

        foreach (PanelCladdingSegmentCoordinate segment in topology.MissingSegments)
        {
            if (!TrySegmentBitIndex(
                segment,
                horizontalTrackCount,
                verticalTrackCount,
                out int bitIndex))
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                    $"PANEL_CLADDING_SEGMENT_MASK_COORDINATE_INVALID: {segment.Axis}:{segment.Track}:{segment.Bay}");
            }
            segmentBits[bitIndex] = false;
            missing.Add(segment);
        }

        foreach (PanelCladdingSegmentCoordinate segment in topology.HiddenSegments)
        {
            if (!TrySegmentBitIndex(
                segment,
                horizontalTrackCount,
                verticalTrackCount,
                out int bitIndex))
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                    $"PANEL_CLADDING_HIDE_MASK_COORDINATE_INVALID: {segment.Axis}:{segment.Track}:{segment.Bay}");
            }
            if (missing.Contains(segment))
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                    $"PANEL_CLADDING_HIDE_MASK_CROSSES_MISSING_SEGMENT: {segment.Axis}:{segment.Track}:{segment.Bay}");
            }
            hideBits[bitIndex] = true;
            hidden.Add(segment);
        }

        foreach (PanelCladdingMergeRun run in topology.MergeRuns)
        {
            int trackCount = run.Axis == PanelCladdingTopologyAxis.Horizontal
                ? horizontalTrackCount
                : verticalTrackCount;
            int bayCount = run.Axis == PanelCladdingTopologyAxis.Horizontal ? columnCount : rowCount;
            if (run.Track < 0 || run.Track >= trackCount || run.StartBay < 0 ||
                run.EndBay <= run.StartBay || run.EndBay >= bayCount)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                    $"PANEL_CLADDING_MERGE_MASK_RUN_INVALID: {run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}");
            }
            for (int bay = run.StartBay; bay <= run.EndBay; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay);
                if (missing.Contains(coordinate))
                {
                    return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                        $"PANEL_CLADDING_MERGE_MASK_CROSSES_MISSING_SEGMENT: {run.Axis}:{run.Track}:{bay}");
                }
            }
            int hiddenCount = Enumerable.Range(run.StartBay, run.EndBay - run.StartBay + 1)
                .Count(bay => hidden.Contains(new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay)));
            if (hiddenCount > 0 && hiddenCount < run.EndBay - run.StartBay + 1)
            {
                return OperationResponse<PanelCladdingTopologyPayloads>.Fail(
                    $"PANEL_CLADDING_MERGE_MASK_PARTIALLY_HIDDEN: {run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}");
            }
            for (int junction = run.StartBay; junction < run.EndBay; junction++)
            {
                mergeBits[MergeBitIndex(
                    run.Axis,
                    run.Track,
                    junction,
                    horizontalTrackCount,
                    verticalTrackCount)] = true;
            }
        }

        return OperationResponse<PanelCladdingTopologyPayloads>.Ok(new PanelCladdingTopologyPayloads
        {
            SegmentMask = EncodeMask(
                SegmentPayloadKind,
                horizontalTrackCount,
                verticalTrackCount,
                columnCount,
                rowCount,
                segmentBits),
            MergeMask = EncodeMask(
                MergePayloadKind,
                horizontalTrackCount,
                verticalTrackCount,
                columnCount,
                rowCount,
                mergeBits),
            HideMask = EncodeMask(
                HidePayloadKind,
                horizontalTrackCount,
                verticalTrackCount,
                columnCount,
                rowCount,
                hideBits)
        });
    }

    public OperationResponse<IReadOnlyDictionary<string, string>> EncodeNonDefaultTopology(
        PanelCladdingTopologyState topology,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        OperationResponse<PanelCladdingTopologyPayloads> encoded = EncodeTopology(
            topology,
            horizontalTrackCount,
            verticalTrackCount);
        if (!encoded.Success || encoded.Data is null)
        {
            return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(encoded.Message);
        }

        var writes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (topology.MissingSegments.Count > 0)
        {
            writes[SegmentMaskKey] = encoded.Data.SegmentMask;
        }
        if (topology.MergeRuns.Count > 0)
        {
            writes[MergeMaskKey] = encoded.Data.MergeMask;
        }
        if (topology.HiddenSegments.Count > 0)
        {
            writes[HideMaskKey] = encoded.Data.HideMask;
        }
        return OperationResponse<IReadOnlyDictionary<string, string>>.Ok(writes);
    }

    public static bool TopologyPersistenceMatches(
        IReadOnlyDictionary<string, string> userText,
        IReadOnlyDictionary<string, string> expectedWrites)
    {
        foreach (string topologyKey in new[] { SegmentMaskKey, MergeMaskKey, HideMaskKey })
        {
            KeyValuePair<string, string>[] stored = userText
                .Where(item => string.Equals(item.Key, topologyKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            bool expected = expectedWrites.TryGetValue(topologyKey, out string? expectedValue);
            if (!expected)
            {
                if (stored.Length > 0)
                {
                    return false;
                }
                continue;
            }
            if (stored.Length != 1 ||
                !string.Equals(stored[0].Value, expectedValue, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasNonblankValue(
        IReadOnlyDictionary<string, string> userText,
        string requestedKey) =>
        userText.Any(item =>
            string.Equals(item.Key, requestedKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.Value));

    public OperationResponse<PanelCladdingTopologyState> DecodeTopology(
        IReadOnlyDictionary<string, string> userText,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        OperationResponse dimensions = ValidateTopologyDimensions(horizontalTrackCount, verticalTrackCount);
        if (!dimensions.Success)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(dimensions.Message);
        }
        int columnCount = verticalTrackCount + 1;
        int rowCount = horizontalTrackCount + 1;
        int segmentBitCount = horizontalTrackCount * columnCount + verticalTrackCount * rowCount;
        int mergeBitCount = horizontalTrackCount * Math.Max(0, columnCount - 1) +
            verticalTrackCount * Math.Max(0, rowCount - 1);
        userText.TryGetValue(SegmentMaskKey, out string? segmentPayload);
        userText.TryGetValue(MergeMaskKey, out string? mergePayload);
        userText.TryGetValue(HideMaskKey, out string? hidePayload);
        OperationResponse<bool[]> segmentBits = DecodeMask(
            segmentPayload,
            SegmentPayloadKind,
            horizontalTrackCount,
            verticalTrackCount,
            columnCount,
            rowCount,
            segmentBitCount,
            defaultValue: true);
        if (!segmentBits.Success || segmentBits.Data is null)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(segmentBits.Message);
        }
        OperationResponse<bool[]> mergeBits = DecodeMask(
            mergePayload,
            MergePayloadKind,
            horizontalTrackCount,
            verticalTrackCount,
            columnCount,
            rowCount,
            mergeBitCount,
            defaultValue: false);
        if (!mergeBits.Success || mergeBits.Data is null)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(mergeBits.Message);
        }
        OperationResponse<bool[]> hideBits = DecodeMask(
            hidePayload,
            HidePayloadKind,
            horizontalTrackCount,
            verticalTrackCount,
            columnCount,
            rowCount,
            segmentBitCount,
            defaultValue: false);
        if (!hideBits.Success || hideBits.Data is null)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(hideBits.Message);
        }

        var missing = new HashSet<PanelCladdingSegmentCoordinate>();
        for (int track = 0; track < horizontalTrackCount; track++)
        {
            for (int bay = 0; bay < columnCount; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, track, bay);
                if (!segmentBits.Data[track * columnCount + bay])
                {
                    missing.Add(coordinate);
                }
            }
        }
        int verticalSegmentBase = horizontalTrackCount * columnCount;
        for (int track = 0; track < verticalTrackCount; track++)
        {
            for (int bay = 0; bay < rowCount; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, track, bay);
                if (!segmentBits.Data[verticalSegmentBase + track * rowCount + bay])
                {
                    missing.Add(coordinate);
                }
            }
        }

        var hidden = new HashSet<PanelCladdingSegmentCoordinate>();
        for (int track = 0; track < horizontalTrackCount; track++)
        {
            for (int bay = 0; bay < columnCount; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, track, bay);
                if (hideBits.Data[track * columnCount + bay])
                {
                    if (missing.Contains(coordinate))
                    {
                        return OperationResponse<PanelCladdingTopologyState>.Fail(
                            $"PANEL_CLADDING_HIDE_MASK_CROSSES_MISSING_SEGMENT: {coordinate.Axis}:{track}:{bay}");
                    }
                    hidden.Add(coordinate);
                }
            }
        }
        for (int track = 0; track < verticalTrackCount; track++)
        {
            for (int bay = 0; bay < rowCount; bay++)
            {
                var coordinate = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, track, bay);
                if (hideBits.Data[verticalSegmentBase + track * rowCount + bay])
                {
                    if (missing.Contains(coordinate))
                    {
                        return OperationResponse<PanelCladdingTopologyState>.Fail(
                            $"PANEL_CLADDING_HIDE_MASK_CROSSES_MISSING_SEGMENT: {coordinate.Axis}:{track}:{bay}");
                    }
                    hidden.Add(coordinate);
                }
            }
        }

        var runs = new List<PanelCladdingMergeRun>();
        OperationResponse horizontalRuns = DecodeMergeRuns(
            PanelCladdingTopologyAxis.Horizontal,
            horizontalTrackCount,
            columnCount,
            0,
            mergeBits.Data,
            missing,
            runs);
        if (!horizontalRuns.Success)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(horizontalRuns.Message);
        }
        int horizontalMergeCount = horizontalTrackCount * Math.Max(0, columnCount - 1);
        OperationResponse verticalRuns = DecodeMergeRuns(
            PanelCladdingTopologyAxis.Vertical,
            verticalTrackCount,
            rowCount,
            horizontalMergeCount,
            mergeBits.Data,
            missing,
            runs);
        if (!verticalRuns.Success)
        {
            return OperationResponse<PanelCladdingTopologyState>.Fail(verticalRuns.Message);
        }
        foreach (PanelCladdingMergeRun run in runs)
        {
            int atomCount = run.EndBay - run.StartBay + 1;
            int hiddenCount = Enumerable.Range(run.StartBay, atomCount)
                .Count(bay => hidden.Contains(new PanelCladdingSegmentCoordinate(run.Axis, run.Track, bay)));
            if (hiddenCount > 0 && hiddenCount < atomCount)
            {
                return OperationResponse<PanelCladdingTopologyState>.Fail(
                    $"PANEL_CLADDING_MERGE_MASK_PARTIALLY_HIDDEN: {run.Axis}:{run.Track}:{run.StartBay}-{run.EndBay}");
            }
        }
        return OperationResponse<PanelCladdingTopologyState>.Ok(new PanelCladdingTopologyState
        {
            MissingSegments = missing
                .OrderBy(item => item.Axis)
                .ThenBy(item => item.Track)
                .ThenBy(item => item.Bay)
                .ToArray(),
            HiddenSegments = hidden
                .OrderBy(item => item.Axis)
                .ThenBy(item => item.Track)
                .ThenBy(item => item.Bay)
                .ToArray(),
            MergeRuns = runs.ToArray()
        });
    }

    public static string GetHorizontalOffsetKey(int index)
    {
        return $"CW_2.03_OFFSET_H{index}";
    }

    public static string GetVerticalOffsetKey(int index)
    {
        return $"CW_2.04_OFFSET_V{index}";
    }

    public static double NormalizeOffset(double value)
    {
        return Math.Round(value, OffsetDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static string FormatOffset(double value)
    {
        return NormalizeOffset(value).ToString("0.#####", CultureInfo.InvariantCulture);
    }

    public bool AreOffsetsCanonicallyStored(
        IReadOnlyDictionary<string, string> userText,
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets)
    {
        int storedOffsetCount = userText.Keys.Count(IsOffsetKey);
        if (storedOffsetCount != horizontalOffsets.Count + verticalOffsets.Count)
        {
            return false;
        }
        for (int index = 0; index < horizontalOffsets.Count; index++)
        {
            if (!HasCanonicalOffset(
                userText,
                GetHorizontalOffsetKey(index),
                horizontalOffsets[index]))
            {
                return false;
            }
        }
        for (int index = 0; index < verticalOffsets.Count; index++)
        {
            if (!HasCanonicalOffset(
                userText,
                GetVerticalOffsetKey(index),
                verticalOffsets[index]))
            {
                return false;
            }
        }
        return true;
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

    public static string FormatUnitDimension(double value)
    {
        return NormalizeOffset(value).ToString("F5", CultureInfo.InvariantCulture);
    }

    private static OperationResponse ValidateTopologyDimensions(int horizontalTrackCount, int verticalTrackCount)
    {
        if (horizontalTrackCount < 0 || verticalTrackCount < 0 ||
            horizontalTrackCount >= ushort.MaxValue || verticalTrackCount >= ushort.MaxValue)
        {
            return OperationResponse.Fail("PANEL_CLADDING_TOPOLOGY_DIMENSIONS_INVALID");
        }
        int columnCount = verticalTrackCount + 1;
        int rowCount = horizontalTrackCount + 1;
        long segmentBits = (long)horizontalTrackCount * columnCount + (long)verticalTrackCount * rowCount;
        long mergeBits = (long)horizontalTrackCount * Math.Max(0, columnCount - 1) +
            (long)verticalTrackCount * Math.Max(0, rowCount - 1);
        if (segmentBits > MaxTopologyBitCount || mergeBits > MaxTopologyBitCount)
        {
            return OperationResponse.Fail("PANEL_CLADDING_TOPOLOGY_DIMENSIONS_INVALID");
        }
        return OperationResponse.Ok();
    }

    private static bool TrySegmentBitIndex(
        PanelCladdingSegmentCoordinate segment,
        int horizontalTrackCount,
        int verticalTrackCount,
        out int bitIndex)
    {
        int columnCount = verticalTrackCount + 1;
        int rowCount = horizontalTrackCount + 1;
        if (segment.Axis == PanelCladdingTopologyAxis.Horizontal)
        {
            if (segment.Track < 0 || segment.Track >= horizontalTrackCount ||
                segment.Bay < 0 || segment.Bay >= columnCount)
            {
                bitIndex = -1;
                return false;
            }
            bitIndex = segment.Track * columnCount + segment.Bay;
            return true;
        }
        if (segment.Track < 0 || segment.Track >= verticalTrackCount || segment.Bay < 0 || segment.Bay >= rowCount)
        {
            bitIndex = -1;
            return false;
        }
        bitIndex = horizontalTrackCount * columnCount + segment.Track * rowCount + segment.Bay;
        return true;
    }

    private static int MergeBitIndex(
        PanelCladdingTopologyAxis axis,
        int track,
        int junction,
        int horizontalTrackCount,
        int verticalTrackCount)
    {
        int columnCount = verticalTrackCount + 1;
        int rowCount = horizontalTrackCount + 1;
        int horizontalJunctions = Math.Max(0, columnCount - 1);
        if (axis == PanelCladdingTopologyAxis.Horizontal)
        {
            return track * horizontalJunctions + junction;
        }
        int verticalJunctions = Math.Max(0, rowCount - 1);
        return horizontalTrackCount * horizontalJunctions + track * verticalJunctions + junction;
    }

    private static string EncodeMask(
        byte kind,
        int horizontalTrackCount,
        int verticalTrackCount,
        int columnCount,
        int rowCount,
        IReadOnlyList<bool> bits)
    {
        byte[] bytes = new byte[TopologyHeaderLength + (bits.Count + 7) / 8];
        bytes[0] = TopologyPayloadVersion;
        bytes[1] = kind;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), checked((ushort)horizontalTrackCount));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4, 2), checked((ushort)verticalTrackCount));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6, 2), checked((ushort)columnCount));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), checked((ushort)rowCount));
        for (int index = 0; index < bits.Count; index++)
        {
            if (bits[index])
            {
                bytes[TopologyHeaderLength + index / 8] |= (byte)(1 << (index % 8));
            }
        }
        return Convert.ToBase64String(bytes);
    }

    private static OperationResponse<bool[]> DecodeMask(
        string? payload,
        byte expectedKind,
        int horizontalTrackCount,
        int verticalTrackCount,
        int columnCount,
        int rowCount,
        int bitCount,
        bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return OperationResponse<bool[]>.Ok(Enumerable.Repeat(defaultValue, bitCount).ToArray());
        }
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload.Trim());
        }
        catch (FormatException)
        {
            return OperationResponse<bool[]>.Fail("PANEL_CLADDING_TOPOLOGY_MASK_BASE64_INVALID");
        }
        int expectedLength = TopologyHeaderLength + (bitCount + 7) / 8;
        if (bytes.Length != expectedLength || bytes.Length < TopologyHeaderLength)
        {
            return OperationResponse<bool[]>.Fail("PANEL_CLADDING_TOPOLOGY_MASK_LENGTH_INVALID");
        }
        if (bytes[0] != TopologyPayloadVersion || bytes[1] != expectedKind)
        {
            return OperationResponse<bool[]>.Fail("PANEL_CLADDING_TOPOLOGY_MASK_HEADER_INVALID");
        }
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)) != horizontalTrackCount ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2)) != verticalTrackCount ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2)) != columnCount ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2)) != rowCount)
        {
            return OperationResponse<bool[]>.Fail("PANEL_CLADDING_TOPOLOGY_MASK_DIMENSIONS_MISMATCH");
        }
        int remainder = bitCount % 8;
        if (remainder != 0)
        {
            byte unusedMask = (byte)~((1 << remainder) - 1);
            if ((bytes[^1] & unusedMask) != 0)
            {
                return OperationResponse<bool[]>.Fail("PANEL_CLADDING_TOPOLOGY_MASK_PADDING_INVALID");
            }
        }
        var bits = new bool[bitCount];
        for (int index = 0; index < bitCount; index++)
        {
            bits[index] = (bytes[TopologyHeaderLength + index / 8] & (1 << (index % 8))) != 0;
        }
        return OperationResponse<bool[]>.Ok(bits);
    }

    private static OperationResponse DecodeMergeRuns(
        PanelCladdingTopologyAxis axis,
        int trackCount,
        int bayCount,
        int bitBase,
        IReadOnlyList<bool> mergeBits,
        IReadOnlySet<PanelCladdingSegmentCoordinate> missing,
        ICollection<PanelCladdingMergeRun> runs)
    {
        int junctionCount = Math.Max(0, bayCount - 1);
        for (int track = 0; track < trackCount; track++)
        {
            int junction = 0;
            while (junction < junctionCount)
            {
                int index = bitBase + track * junctionCount + junction;
                if (!mergeBits[index])
                {
                    junction++;
                    continue;
                }
                int start = junction;
                while (junction < junctionCount && mergeBits[bitBase + track * junctionCount + junction])
                {
                    var first = new PanelCladdingSegmentCoordinate(axis, track, junction);
                    var second = new PanelCladdingSegmentCoordinate(axis, track, junction + 1);
                    if (missing.Contains(first) || missing.Contains(second))
                    {
                        return OperationResponse.Fail(
                            $"PANEL_CLADDING_MERGE_MASK_CROSSES_MISSING_SEGMENT: {axis}:{track}:{junction}");
                    }
                    junction++;
                }
                runs.Add(new PanelCladdingMergeRun(axis, track, start, junction));
            }
        }
        return OperationResponse.Ok();
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
        var ordered = new List<double>(byIndex.Count);
        foreach ((int index, double value) in byIndex)
        {
            if (index != expected)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_GAPPED_{label}_INDICES: expected {label}{expected}, found {label}{index}.");
            }

            ordered.Add(value);
            expected++;
        }
        return ValidateOffsets(ordered, label, extent, tolerance);
    }

    private static bool HasCanonicalOffset(
        IReadOnlyDictionary<string, string> userText,
        string key,
        double value)
    {
        return userText.TryGetValue(key, out string? stored) && string.Equals(
            stored?.Trim(),
            FormatOffset(value),
            StringComparison.Ordinal);
    }

    private static OperationResponse<IReadOnlyList<double>> ValidateOffsets(
        IReadOnlyList<double> values,
        string label,
        double extent,
        double tolerance)
    {
        double previous = double.NegativeInfinity;
        var ordered = new List<double>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            double value = NormalizeOffset(values[index]);
            if (!double.IsFinite(value))
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_INVALID_{label}_VALUE: {label}{index} is not finite.");
            }
            if (value <= tolerance || value >= extent - tolerance)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_{label}_OUTSIDE_EXTENT: {label}{index}={FormatOffset(value)} is outside the panel interior.");
            }
            if (value <= previous + tolerance)
            {
                return OperationResponse<IReadOnlyList<double>>.Fail(
                    $"PANEL_CLADDING_NON_MONOTONIC_{label}: {label}{index} is not strictly greater than the prior offset.");
            }
            ordered.Add(value);
            previous = value;
        }
        return OperationResponse<IReadOnlyList<double>>.Ok(ordered);
    }

    [GeneratedRegex(@"^CW_2\.03_OFFSET_H(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalOffsetRegex();

    [GeneratedRegex(@"^CW_2\.04_OFFSET_V(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VerticalOffsetRegex();

    [GeneratedRegex(@"^CW_\d+\.\d{2}_CLADDING_\d+[A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CladdingCellRegex();

    [GeneratedRegex(@"^\d+[A-Z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CellLabelRegex();
}

