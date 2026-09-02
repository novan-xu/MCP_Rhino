using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingMatchParentFidelitySmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);

        IReadOnlyDictionary<string, string> canonical = BuildSource(keys, horizontal: true,
            new Dictionary<string, string>()
        {
            ["0A"] = "MPL-001",
            ["0B"] = "MPL-001",
            ["1A"] = "0A",
            ["1B"] = "0B"
        });
        PanelCladdingMatchTargetPlan canonicalPlan = Required(planner.CreatePlan(
            Snapshot(SourceId, canonical),
            [Snapshot(TargetId, BuildTarget(keys, horizontal: true))])).Targets.Single();
        AssertWrite(canonicalPlan, keys, "0A", "MPL-001");
        AssertWrite(canonicalPlan, keys, "0B", "MPL-001");
        AssertWrite(canonicalPlan, keys, "1A", "0A");
        AssertWrite(canonicalPlan, keys, "1B", "0B");
        Console.WriteLine("[OK] the reported 2x2 parent-cell configuration transfers exactly.");

        IReadOnlyDictionary<string, string> nonCanonical = BuildSource(keys, horizontal: false,
            new Dictionary<string, string>()
        {
            ["0A"] = "1A",
            ["1A"] = "mpl-001"
        });
        PanelCladdingMatchTargetPlan nonCanonicalPlan = Required(planner.CreatePlan(
            Snapshot(SourceId, nonCanonical),
            [Snapshot(TargetId, BuildTarget(keys, horizontal: false))])).Targets.Single();
        AssertWrite(nonCanonicalPlan, keys, "0A", "1A");
        AssertWrite(nonCanonicalPlan, keys, "1A", "MPL-001");
        Console.WriteLine("[OK] PCMatchSrf preserves source parent direction instead of canonicalizing it.");

        IReadOnlyDictionary<string, string> committed =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [PanelCladdingKeyService.GetCellKey(0, "A")] = "OLD",
                    ["CustomNote"] = "preserve"
                },
                nonCanonicalPlan);
        Require(committed[PanelCladdingKeyService.GetCellKey(0, "A")] == "1A" &&
                committed[PanelCladdingKeyService.GetCellKey(1, "A")] == "MPL-001" &&
                committed["CustomNote"] == "preserve",
            "The live adapter's shared delete/write transformation changed the planned parent graph.");
        Console.WriteLine("[OK] the live adapter's shared attribute transformation retains the exact values.");
    }

    private static IReadOnlyDictionary<string, string> BuildSource(
        PanelCladdingKeyService keys,
        bool horizontal,
        IReadOnlyDictionary<string, string> assignments)
    {
        int horizontalCount = horizontal ? 1 : 0;
        PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState(), horizontalCount, 1));
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "50",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-PARENT",
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:parent-fidelity",
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };
        if (horizontal)
        {
            text[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "50";
        }
        foreach ((string label, string value) in assignments)
        {
            int column = int.Parse(label[..1]);
            text[PanelCladdingKeyService.GetCellKey(column, label[1..])] = value;
        }
        return text;
    }

    private static IReadOnlyDictionary<string, string> BuildTarget(
        PanelCladdingKeyService keys,
        bool horizontal)
    {
        int horizontalCount = horizontal ? 1 : 0;
        PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState(), horizontalCount, 1));
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "60",
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };
        if (horizontal)
        {
            text[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "40";
        }
        return text;
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid id,
        IReadOnlyDictionary<string, string> text) => new()
    {
        ObjectId = id,
        Geometry = new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = 100d,
            Height = 100d,
            ModelTolerance = 0.001d
        },
        UserText = text
    };

    private static void AssertWrite(
        PanelCladdingMatchTargetPlan plan,
        PanelCladdingKeyService keys,
        string label,
        string expected)
    {
        int column = int.Parse(label[..1]);
        string key = PanelCladdingKeyService.GetCellKey(column, label[1..]);
        Require(plan.UserTextWrites.TryGetValue(key, out string? actual) && actual == expected,
            $"Expected {key}={expected}, got {actual ?? "<missing>"}.");
    }

    private static T Required<T>(OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
