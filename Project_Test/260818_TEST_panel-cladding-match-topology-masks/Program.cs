using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingMatchTopologyMasksSmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        PanelCladdingTopologyState sourceTopology = new()
        {
            MissingSegments = new[]
            {
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            },
            MergeRuns = new[]
            {
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 1, 0, 1)
            }
        };
        PanelCladdingTopologyPayloads sourceMasks = Required(
            keys.EncodeTopology(sourceTopology, 1, 2),
            "Encode source masks");
        PanelCladdingTopologyPayloads staleTargetMasks = Required(
            keys.EncodeTopology(new PanelCladdingTopologyState(), 1, 2),
            "Encode stale target masks");

        PanelCladdingMatchPanelSnapshot source = Snapshot(SourceId, SourceUserText(sourceMasks));
        PanelCladdingMatchPanelSnapshot target = Snapshot(
            TargetId,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "25",
                [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "20",
                [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "65",
                [PanelCladdingKeyService.SegmentMaskKey] = staleTargetMasks.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = staleTargetMasks.MergeMask,
                ["CW_1.01_PID"] = "TARGET",
                ["CustomNote"] = "preserve"
            });

        PanelCladdingMatchPlan plan = Required(
            planner.CreatePlan(source, new[] { target }),
            "Create topology-mask match plan");
        PanelCladdingMatchTargetPlan targetPlan = plan.Targets.Single();
        Require(!targetPlan.UserTextWrites.ContainsKey(PanelCladdingKeyService.SegmentMaskKey) &&
                !targetPlan.UserTextWrites.ContainsKey(PanelCladdingKeyService.MergeMaskKey),
            "PCMatchSrf must not copy source topology masks.");
        Require(!targetPlan.UserTextDeletes.Contains(PanelCladdingKeyService.SegmentMaskKey, StringComparer.OrdinalIgnoreCase) &&
                !targetPlan.UserTextDeletes.Contains(PanelCladdingKeyService.MergeMaskKey, StringComparer.OrdinalIgnoreCase),
            "PCMatchSrf must not delete target topology masks.");
        Require(!targetPlan.UserTextDeletes.Any(IsOffsetKey) &&
                !targetPlan.UserTextWrites.Keys.Any(IsOffsetKey),
            "PCMatch must preserve target H/V offsets.");
        Require(!targetPlan.UserTextDeletes.Contains("CW_1.01_PID", StringComparer.OrdinalIgnoreCase) &&
                !targetPlan.UserTextDeletes.Contains("CustomNote", StringComparer.OrdinalIgnoreCase),
            "Target identity or unrelated data was scheduled for deletion.");

        IReadOnlyDictionary<string, string> applied = PanelCladdingMatchPlanningService.ApplyUserTextPlan(
            target.UserText,
            targetPlan);
        PanelCladdingTopologyState decoded = Required(
            keys.DecodeTopology(applied, 1, 2),
            "Decode preserved target masks");
        Require(decoded.MissingSegments.Count == 0 && decoded.MergeRuns.Count == 0,
            "PCMatchSrf changed the target extrusion topology.");
        Console.WriteLine("[OK] PCMatchSrf ignores source masks and preserves target masks and H/V offsets.");

        var legacySourceText = new Dictionary<string, string>(SourceUserText(sourceMasks), StringComparer.OrdinalIgnoreCase);
        legacySourceText.Remove(PanelCladdingKeyService.SegmentMaskKey);
        legacySourceText.Remove(PanelCladdingKeyService.MergeMaskKey);
        PanelCladdingMatchPlan legacyPlan = Required(
            planner.CreatePlan(Snapshot(SourceId, legacySourceText), new[] { Snapshot(TargetId, TargetGrid()) }),
            "Create legacy-source match plan");
        Require(!legacyPlan.Targets.Single().UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "A source without stored masks must not invent topology writes.");
        Console.WriteLine("[OK] sources without masks remain valid and emit no topology writes.");

        OperationResponse<PanelCladdingMatchPlan> mismatch = planner.CreatePlan(
            source,
            new[]
            {
                Snapshot(TargetId, new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "25"
                })
            });
        Require(!mismatch.Success && mismatch.Message.Contains("GEOMETRY_MISMATCH", StringComparison.Ordinal),
            "A different target lattice must fail before mask transfer.");
        Console.WriteLine("[OK] mask transfer remains fail-closed for different grid dimensions.");
    }

    private static IReadOnlyDictionary<string, string> SourceUserText(PanelCladdingTopologyPayloads masks)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "40",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "30",
            [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "70",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-3X2-MASKTEST",
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:masktest",
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };
        for (int column = 0; column < 3; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                values[PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row))] =
                    $"MAT{column}{row}";
            }
        }
        return values;
    }

    private static IReadOnlyDictionary<string, string> TargetGrid() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "25",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "20",
            [PanelCladdingKeyService.GetVerticalOffsetKey(1)] = "65"
        };

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid id,
        IReadOnlyDictionary<string, string> userText) => new()
    {
        ObjectId = id,
        Geometry = new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = 100d,
            Height = 100d,
            ModelTolerance = 0.001d
        },
        UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
    };

    private static bool IsOffsetKey(string key) =>
        key.Contains("OFFSET_H", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("OFFSET_V", StringComparison.OrdinalIgnoreCase);

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
