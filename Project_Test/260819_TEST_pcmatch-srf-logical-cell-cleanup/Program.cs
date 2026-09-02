using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCMatchSrfLogicalCellCleanupSmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyPayloads sourceMasks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState
            {
                MissingSegments =
                [
                    new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                    new(PanelCladdingTopologyAxis.Horizontal, 1, 1)
                ]
            },
            horizontalTrackCount: 2,
            verticalTrackCount: 1));
        PanelCladdingTopologyPayloads targetMasks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState(),
            horizontalTrackCount: 2,
            verticalTrackCount: 1));

        var sourceText = new Dictionary<string, string>(Grid("30", "70", "50", sourceMasks),
            StringComparer.OrdinalIgnoreCase)
        {
            [CellKey("0A")] = "MPL-001",
            [CellKey("1A")] = "0A",
            [CellKey("1B")] = "TER-001"
        };
        var targetText = new Dictionary<string, string>(Grid("25", "80", "65", targetMasks),
            StringComparer.OrdinalIgnoreCase)
        {
            [CellKey("0B")] = PanelCladdingKeyService.PersistedBlankCellValue,
            [CellKey("1C")] = PanelCladdingKeyService.PersistedBlankCellValue,
            [PanelCladdingKeyService.TypeCodeKey] = "TARGET-TYPE-PRESERVE",
            [PanelCladdingKeyService.SignatureKey] = "target-signature-preserve",
            ["CustomNote"] = "preserve"
        };

        PanelCladdingMatchTargetPlan targetPlan = Required(
            new PanelCladdingMatchPlanningService(keys).CreatePlan(
                Snapshot(SourceId, sourceText),
                [Snapshot(TargetId, targetText)])).Targets.Single();

        string hidden0B = CellKey("0B");
        string hidden1C = CellKey("1C");
        Require(targetPlan.UserTextDeletes.Contains(hidden0B, StringComparer.OrdinalIgnoreCase) &&
                targetPlan.UserTextDeletes.Contains(hidden1C, StringComparer.OrdinalIgnoreCase),
            "Stale hidden target cells were not scheduled for deletion.");
        Require(targetPlan.UserTextWrites[hidden0B] == "0A" &&
                targetPlan.UserTextWrites[hidden1C] == "1B",
            "Target-visible cells did not inherit their source regions through parent references.");
        Require(targetPlan.UserTextWrites[CellKey("0A")] == "MPL-001" &&
                targetPlan.UserTextWrites[CellKey("1A")] == "0A" &&
                targetPlan.UserTextWrites[CellKey("1B")] == "TER-001",
            "Surviving material or remapped parent-cell values are incorrect.");
        Require(targetPlan.UserTextWrites[CellKey("0C")] ==
                PanelCladdingKeyService.PersistedBlankCellValue,
            "A surviving unassigned logical cell was not retained as a blank value.");

        IReadOnlyDictionary<string, string> applied =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(targetText, targetPlan);
        Require(applied[hidden0B] == "0A" && applied[hidden1C] == "1B",
            "Applying PCMatchSrf did not replace stale target cells with mapped parent references.");
        Require(applied[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] == "25" &&
                applied[PanelCladdingKeyService.GetHorizontalOffsetKey(1)] == "80" &&
                applied[PanelCladdingKeyService.GetVerticalOffsetKey(0)] == "65" &&
                applied[PanelCladdingKeyService.SegmentMaskKey] == targetMasks.SegmentMask &&
                applied[PanelCladdingKeyService.MergeMaskKey] == targetMasks.MergeMask &&
                applied[PanelCladdingKeyService.TypeCodeKey] == "TARGET-TYPE-PRESERVE" &&
                !applied.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                applied["CustomNote"] == "preserve",
            "PCMatchSrf changed target-owned layout, topology, type metadata, unrelated text, or retained Signature.");

        Console.WriteLine("[OK] PCMatchSrf remaps source-collapsed regions onto target-visible 0B/1C cells.");
        Console.WriteLine("[OK] surviving blanks and parent references remain logical-cell correct.");
        Console.WriteLine("[OK] target offsets, masks, metadata, and unrelated attributes remain unchanged.");
    }

    private static IReadOnlyDictionary<string, string> Grid(
        string horizontal0,
        string horizontal1,
        string vertical0,
        PanelCladdingTopologyPayloads masks) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = horizontal0,
            [PanelCladdingKeyService.GetHorizontalOffsetKey(1)] = horizontal1,
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = vertical0,
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };

    private static string CellKey(string label)
    {
        int column = int.Parse(label[..1]);
        return PanelCladdingKeyService.GetCellKey(column, label[1..]);
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
