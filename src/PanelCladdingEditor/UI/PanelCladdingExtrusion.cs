using System.Globalization;

namespace PanelCladdingEditor.UI;

public enum PanelEditorView
{
    Cladding,
    Extrusion
}

public enum PanelExtrusionAxis
{
    Horizontal,
    Vertical
}

public sealed record PanelExtrusionMergeGroup(
    string Id,
    PanelExtrusionAxis Axis,
    double Offset,
    IReadOnlyList<string> AtomicIds);

public sealed record PanelExtrusionSegment(
    string Id,
    string Code,
    string Kind,
    PanelExtrusionAxis Axis,
    double Offset,
    double Start,
    double End,
    IReadOnlyList<string> AtomicIds)
{
    public bool IsFrame => string.Equals(Kind, "frame", StringComparison.Ordinal);
    public bool IsMerged => string.Equals(Kind, "merged", StringComparison.Ordinal);
}

public static class PanelExtrusionTopology
{
    public static IReadOnlyList<PanelExtrusionSegment> Build(
        double panelWidth,
        double panelHeight,
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        IReadOnlyList<PanelExtrusionMergeGroup> mergedGroups,
        IReadOnlySet<string> deletedAtomicIds)
    {
        var frames = new List<PanelExtrusionSegment>
        {
            Frame("FRM_0", PanelExtrusionAxis.Horizontal, 0d, panelWidth),
            Frame("FRM_1", PanelExtrusionAxis.Horizontal, panelHeight, panelWidth),
            Frame("FRM_2", PanelExtrusionAxis.Vertical, 0d, panelHeight),
            Frame("FRM_3", PanelExtrusionAxis.Vertical, panelWidth, panelHeight)
        };
        double[] xCuts = [0d, .. verticalOffsets, panelWidth];
        double[] yCuts = [0d, .. horizontalOffsets, panelHeight];
        var atomic = new List<PanelExtrusionSegment>();
        for (int line = 0; line < horizontalOffsets.Count; line++)
        {
            for (int bay = 0; bay < xCuts.Length - 1; bay++)
            {
                string id = AtomicId(PanelExtrusionAxis.Horizontal, horizontalOffsets[line], bay);
                atomic.Add(new PanelExtrusionSegment(
                    id,
                    $"INT_{Alpha(line)}{bay}",
                    "segment",
                    PanelExtrusionAxis.Horizontal,
                    horizontalOffsets[line],
                    xCuts[bay],
                    xCuts[bay + 1],
                    [id]));
            }
        }
        for (int line = 0; line < verticalOffsets.Count; line++)
        {
            for (int bay = 0; bay < yCuts.Length - 1; bay++)
            {
                string id = AtomicId(PanelExtrusionAxis.Vertical, verticalOffsets[line], bay);
                atomic.Add(new PanelExtrusionSegment(
                    id,
                    $"INT_{line}{Alpha(bay)}",
                    "segment",
                    PanelExtrusionAxis.Vertical,
                    verticalOffsets[line],
                    yCuts[bay],
                    yCuts[bay + 1],
                    [id]));
            }
        }

        var atomicById = atomic.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<PanelExtrusionSegment>();
        foreach (PanelExtrusionMergeGroup group in mergedGroups)
        {
            PanelExtrusionSegment[] members = group.AtomicIds
                .Where(id => !deletedAtomicIds.Contains(id) && atomicById.ContainsKey(id))
                .Select(id => atomicById[id])
                .OrderBy(item => item.Start)
                .ToArray();
            if (members.Length < 2)
            {
                continue;
            }
            foreach (PanelExtrusionSegment member in members)
            {
                claimed.Add(member.Id);
            }
            PanelExtrusionSegment first = members[0];
            PanelExtrusionSegment last = members[^1];
            int lineCount = first.Axis == PanelExtrusionAxis.Horizontal ? xCuts.Length - 1 : yCuts.Length - 1;
            bool wholeLine = members.Length == lineCount && Math.Abs(first.Start) < 1e-7d &&
                Math.Abs(last.End - (first.Axis == PanelExtrusionAxis.Horizontal ? panelWidth : panelHeight)) < 1e-7d;
            string baseLabel = first.Axis == PanelExtrusionAxis.Horizontal
                ? Alpha(IndexOfOffset(horizontalOffsets, first.Offset))
                : IndexOfOffset(verticalOffsets, first.Offset).ToString(CultureInfo.InvariantCulture);
            string range = first.Axis == PanelExtrusionAxis.Horizontal
                ? $"{BayOf(first.Id)}–{BayOf(last.Id)}"
                : $"{Alpha(BayOf(first.Id))}–{Alpha(BayOf(last.Id))}";
            merged.Add(new PanelExtrusionSegment(
                group.Id,
                wholeLine ? $"INT_{baseLabel}" : $"INT_{baseLabel}{range}",
                "merged",
                first.Axis,
                first.Offset,
                first.Start,
                last.End,
                members.Select(item => item.Id).ToArray()));
        }

        frames.AddRange(merged);
        frames.AddRange(atomic.Where(item => !deletedAtomicIds.Contains(item.Id) && !claimed.Contains(item.Id)));
        return frames;
    }

    public static string AtomicId(PanelExtrusionAxis axis, double offset, int bay) =>
        $"{(axis == PanelExtrusionAxis.Horizontal ? 'H' : 'V')}:{offset.ToString("0.#####", CultureInfo.InvariantCulture)}:{bay}";

    public static bool TryParseAtomicId(string id, out PanelExtrusionAxis axis, out double offset, out int bay)
    {
        axis = PanelExtrusionAxis.Horizontal;
        offset = 0d;
        bay = 0;
        string[] parts = id.Split(':');
        if (parts.Length != 3 || (parts[0] != "H" && parts[0] != "V") ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out offset) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out bay))
        {
            return false;
        }
        axis = parts[0] == "H" ? PanelExtrusionAxis.Horizontal : PanelExtrusionAxis.Vertical;
        return true;
    }

    private static PanelExtrusionSegment Frame(string id, PanelExtrusionAxis axis, double offset, double end) =>
        new(id, id, "frame", axis, offset, 0d, end, Array.Empty<string>());

    private static int IndexOfOffset(IReadOnlyList<double> values, double target)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (Math.Abs(values[index] - target) < 1e-5d)
            {
                return index;
            }
        }
        return 0;
    }

    private static int BayOf(string id) =>
        int.TryParse(id.Split(':').LastOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bay) ? bay : 0;

    public static string Alpha(int index)
    {
        string result = string.Empty;
        int value = index + 1;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }
        return result;
    }
}
