using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingMatchParentCellsSmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        IReadOnlyDictionary<string, string> sourceText = SourceText(keys);
        IReadOnlyDictionary<string, string> targetText = TargetText(keys);

        PanelCladdingMatchPlan plan = Required(
            planner.CreatePlan(Snapshot(SourceId, sourceText), new[] { Snapshot(TargetId, targetText) }),
            "Create parent-cell match plan");
        PanelCladdingMatchTargetPlan targetPlan = plan.Targets.Single();
        AssertAssignment(targetPlan.UserTextWrites, 0, "A", "MPL-001");
        AssertAssignment(targetPlan.UserTextWrites, 0, "B", "MPL-001");
        AssertAssignment(targetPlan.UserTextWrites, 1, "A", "0A");
        AssertAssignment(targetPlan.UserTextWrites, 1, "B", "0B");
        Require(!targetPlan.UserTextWrites.Keys.Any(keys.IsOffsetKey) &&
                !targetPlan.UserTextDeletes.Any(keys.IsOffsetKey),
            "Parent-cell matching must not copy or delete target H/V offsets.");
        Require(!targetPlan.UserTextWrites.ContainsKey(PanelCladdingKeyService.SegmentMaskKey) &&
                !targetPlan.UserTextWrites.ContainsKey(PanelCladdingKeyService.MergeMaskKey) &&
                !targetPlan.UserTextDeletes.Contains(PanelCladdingKeyService.SegmentMaskKey, StringComparer.OrdinalIgnoreCase) &&
                !targetPlan.UserTextDeletes.Contains(PanelCladdingKeyService.MergeMaskKey, StringComparer.OrdinalIgnoreCase),
            "PCMatchSrf must preserve target topology masks.");
        Console.WriteLine("[OK] planned writes preserve 0A/0B parent references instead of flattening them.");

        Dictionary<string, string> applied = ApplyPlan(targetText, targetPlan);
        PanelCladdingKeySet matched = Required(
            keys.Parse(applied, panelWidth: 100d, panelHeight: 100d, tolerance: 0.001d),
            "Parse matched target");
        AssertCell(matched, "0A", "MPL-001");
        AssertCell(matched, "0B", "MPL-001");
        AssertCell(matched, "1A", "0A");
        AssertCell(matched, "1B", "0B");

        PanelCladdingRegionSet regions = Required(
            new PanelCladdingRegionService(keys).Resolve(matched.Cells, requirePopulatedCells: true),
            "Resolve matched parent regions");
        Require(regions.Regions.Count == 2 &&
                regions.Regions.All(region => region.MaterialCode == "MPL-001" && region.Cells.Count == 2) &&
                regions.Regions.Select(region => region.OwnerCellLabel).SequenceEqual(new[] { "0A", "0B" }),
            "The matched target did not reconstruct the two source parent regions.");
        Console.WriteLine("[OK] reparsed target reconstructs two MPL-001 regions owned by 0A and 0B.");

        var cycle = new Dictionary<string, string>(sourceText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "1A",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A"
        };
        OperationResponse<PanelCladdingMatchPlan> rejected = planner.CreatePlan(
            Snapshot(SourceId, cycle),
            new[] { Snapshot(TargetId, targetText) });
        Require(!rejected.Success &&
                rejected.Message.Contains("SOURCE_NOT_CONFIGURED", StringComparison.Ordinal) &&
                rejected.Message.Contains("REFERENCE_CYCLE", StringComparison.Ordinal),
            $"Invalid parent cycles must fail before target planning. Got: {rejected.Message}");
        Console.WriteLine("[OK] invalid source parent-reference cycles fail before target mutation.");
    }

    private static IReadOnlyDictionary<string, string> SourceText(PanelCladdingKeyService keys)
    {
        PanelCladdingTopologyPayloads masks = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode source masks");
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "40",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "50",
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "0B",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-2X2-PARENT",
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:parent",
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };
    }

    private static IReadOnlyDictionary<string, string> TargetText(PanelCladdingKeyService keys)
    {
        PanelCladdingTopologyPayloads masks = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 1),
            "Encode target masks");
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "25",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "65",
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask,
            ["CW_1.01_PID"] = "TARGET",
            ["CustomNote"] = "preserve"
        };
    }

    private static Dictionary<string, string> ApplyPlan(
        IReadOnlyDictionary<string, string> current,
        PanelCladdingMatchTargetPlan plan)
    {
        var result = new Dictionary<string, string>(current, StringComparer.OrdinalIgnoreCase);
        foreach (string key in plan.UserTextDeletes)
        {
            result.Remove(key);
        }
        foreach ((string key, string value) in plan.UserTextWrites)
        {
            result[key] = value;
        }
        return result;
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyDictionary<string, string> userText) => new()
    {
        ObjectId = objectId,
        Geometry = new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = 100d,
            Height = 100d,
            ModelTolerance = 0.001d
        },
        UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
    };

    private static void AssertAssignment(
        IReadOnlyDictionary<string, string> writes,
        int column,
        string row,
        string expected)
    {
        string key = PanelCladdingKeyService.GetCellKey(column, row);
        Require(writes.TryGetValue(key, out string? actual) && actual == expected,
            $"Expected {key}={expected}, got {actual ?? "<missing>"}.");
    }

    private static void AssertCell(PanelCladdingKeySet keySet, string label, string expected)
    {
        PanelCladdingCell cell = keySet.Cells.Single(item => item.ShortLabel == label);
        Require(cell.Value == expected, $"Expected matched {label}={expected}, got {cell.Value}.");
    }

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
