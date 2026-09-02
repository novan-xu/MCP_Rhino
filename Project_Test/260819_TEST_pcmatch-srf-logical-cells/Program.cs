using System.IO;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCMatchSrfLogicalCellsSmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetOneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TargetTwoId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static int Main()
    {
        BlankSourceMatchesByCellCodesOnly();
        AssignedParentGraphTransfersExactly();
        DifferentCellCodesMatchWhenPartitionIsRepresentable();
        InvalidParentGraphStillRejectsTheSource();
        string commandSource = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "PanelCladdingEditor",
            "UI",
            "PanelCladdingMatchCommand.cs"));
        Require(commandSource.Contains("EnglishName => \"PCMatchSrf\"", StringComparison.Ordinal) &&
                !commandSource.Contains("EnglishName => \"PCMatch\"", StringComparison.Ordinal),
            "The public Rhino command was not renamed exclusively to PCMatchSrf.");
        Console.WriteLine("[OK] PCMatchSrf is the registered public command name.");
        return 0;
    }

    private static void BlankSourceMatchesByCellCodesOnly()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        PanelCladdingTopologyPayloads sourceMasks = Masks(keys, new PanelCladdingTopologyState
        {
            MissingSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)]
        });
        PanelCladdingTopologyPayloads targetMasks = Masks(keys, new PanelCladdingTopologyState
        {
            MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
        });
        IReadOnlyDictionary<string, string> sourceText = GridText("40", "30", sourceMasks);
        var targetText = new Dictionary<string, string>(GridText("75", "80", targetMasks), StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = PanelCladdingKeyService.PersistedBlankCellValue,
            [PanelCladdingKeyService.TypeCodeKey] = "TARGET-TYPE-PRESERVE",
            [PanelCladdingKeyService.SignatureKey] = "target-signature-preserve",
            ["CustomNote"] = "preserve"
        };

        PanelCladdingMatchTargetPlan plan = Required(planner.CreatePlan(
            Snapshot(SourceId, sourceText, width: 100d, height: 100d),
            [Snapshot(TargetOneId, targetText, width: 160d, height: 240d)])).Targets.Single();

        Require(CellKeys().All(key => plan.UserTextWrites.TryGetValue(key, out string? value) &&
                value == PanelCladdingKeyService.PersistedBlankCellValue),
            "A source without assignments did not map its blank region onto every target cell.");
        Require(plan.UserTextWrites.Keys.All(key => keys.IsCladdingCellKey(key) || string.Equals(
                key,
                PanelCladdingKeyService.CladdingLogicKey,
                StringComparison.OrdinalIgnoreCase)) &&
                plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.CladdingLogicKey),
            "PCMatchSrf planned a write outside cell assignments and their derived logic.");
        Require(plan.UserTextDeletes.All(key => keys.IsCladdingCellKey(key) ||
                string.Equals(key, PanelCladdingKeyService.SignatureKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase)),
            "PCMatchSrf planned a non-cell deletion other than retired Signature keys.");

        IReadOnlyDictionary<string, string> applied =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(targetText, plan);
        Require(applied[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] == "75" &&
                applied[PanelCladdingKeyService.GetVerticalOffsetKey(0)] == "80" &&
                applied[PanelCladdingKeyService.SegmentMaskKey] == targetMasks.SegmentMask &&
                applied[PanelCladdingKeyService.MergeMaskKey] == targetMasks.MergeMask &&
                applied[PanelCladdingKeyService.TypeCodeKey] == "TARGET-TYPE-PRESERVE" &&
                !applied.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                applied["CustomNote"] == "preserve",
            "PCMatchSrf changed target-owned layout, topology, type metadata, unrelated text, or retained Signature.");
        Console.WriteLine("[OK] a blank source region expands across every compatible target physical cell.");
    }

    private static void AssignedParentGraphTransfersExactly()
    {
        var keys = new PanelCladdingKeyService();
        var source = new Dictionary<string, string>(GridText("40", "30", Masks(keys, new PanelCladdingTopologyState())),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "0B"
        };
        PanelCladdingMatchTargetPlan plan = Required(new PanelCladdingMatchPlanningService(keys).CreatePlan(
            Snapshot(SourceId, source),
            [Snapshot(TargetOneId, GridText("65", "55", Masks(keys, new PanelCladdingTopologyState())))])).Targets.Single();

        Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(0, "A")] == "MPL-001" &&
                plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(0, "B")] == "MPL-001" &&
                plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A" &&
                plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "B")] == "0B",
            "PCMatchSrf changed the source material/parent graph.");
        Console.WriteLine("[OK] assigned materials and parent-cell conditions transfer exactly by cell code.");
    }

    private static void DifferentCellCodesMatchWhenPartitionIsRepresentable()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        PanelCladdingMatchPanelSnapshot source = Snapshot(
            SourceId,
            GridText("40", "30", Masks(keys, new PanelCladdingTopologyState())));
        PanelCladdingMatchPanelSnapshot compatible = Snapshot(
            TargetOneId,
            GridText("70", "80", Masks(keys, new PanelCladdingTopologyState())));
        var oneColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "70"
        };
        OperationResponse<PanelCladdingMatchPlan> planned = planner.CreatePlan(
            source,
            [compatible, Snapshot(TargetTwoId, oneColumn)]);
        Require(planned.Success && planned.Data is not null && planned.Data.Targets.Count == 2,
            $"A representable target with fewer raw cells was rejected: {planned.Message}");
        Console.WriteLine("[OK] different raw cell codes match when the source partition remains representable.");
    }

    private static void InvalidParentGraphStillRejectsTheSource()
    {
        var keys = new PanelCladdingKeyService();
        var source = new Dictionary<string, string>(GridText("40", "30", Masks(keys, new PanelCladdingTopologyState())),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "9Z"
        };
        OperationResponse<PanelCladdingMatchPlan> rejected = new PanelCladdingMatchPlanningService(keys).CreatePlan(
            Snapshot(SourceId, source),
            [Snapshot(TargetOneId, GridText("60", "70", Masks(keys, new PanelCladdingTopologyState()))) ]);
        Require(!rejected.Success &&
                rejected.Message.Contains("PANEL_CLADDING_MATCH_SOURCE_NOT_CONFIGURED", StringComparison.Ordinal) &&
                rejected.Message.Contains("REFERENCE_TARGET_NOT_FOUND", StringComparison.Ordinal),
            "An invalid source parent reference was accepted.");
        Console.WriteLine("[OK] invalid parent references remain fail-closed.");
    }

    private static IReadOnlyDictionary<string, string> GridText(
        string horizontal,
        string vertical,
        PanelCladdingTopologyPayloads masks) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = horizontal,
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = vertical,
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };

    private static PanelCladdingTopologyPayloads Masks(
        PanelCladdingKeyService keys,
        PanelCladdingTopologyState topology) =>
        Required(keys.EncodeTopology(topology, horizontalTrackCount: 1, verticalTrackCount: 1));

    private static IEnumerable<string> CellKeys()
    {
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                yield return PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row));
            }
        }
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid id,
        IReadOnlyDictionary<string, string> userText,
        double width = 100d,
        double height = 100d) => new()
    {
        ObjectId = id,
        Geometry = new PanelCladdingMatchGeometryDescriptor
        {
            GeometryClass = PanelGeometryClass.Planar,
            Width = width,
            Height = height,
            ModelTolerance = 0.001d
        },
        UserText = new Dictionary<string, string>(userText, StringComparer.OrdinalIgnoreCase)
    };

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
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
