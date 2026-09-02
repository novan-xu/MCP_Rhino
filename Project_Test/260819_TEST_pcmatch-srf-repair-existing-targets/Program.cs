using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCMatchSrfRepairExistingTargetsSmoke;

internal static class Program
{
    private static readonly Guid SourceId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyPayloads sourceMasks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState
            {
                MissingSegments = new[]
                {
                    new PanelCladdingSegmentCoordinate(
                        PanelCladdingTopologyAxis.Vertical,
                        0,
                        1)
                }
            },
            horizontalTrackCount: 1,
            verticalTrackCount: 1));
        var source = new Dictionary<string, string>(
            GridText(sourceMasks, horizontal: "80", vertical: "45"),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-002"
        };
        PanelCladdingTopologyPayloads targetMasks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState(),
            horizontalTrackCount: 1,
            verticalTrackCount: 1));
        var target = new Dictionary<string, string>(
            GridText(targetMasks, horizontal: "100", vertical: "60"),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "OLD-001",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "OLD-002",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "OLD-003",
            [PanelCladdingKeyService.TypeCodeKey] = "TARGET-TYPE",
            [PanelCladdingKeyService.SignatureKey] = "target-signature",
            ["CustomNote"] = "preserve"
        };

        PanelCladdingMatchTargetPlan plan = Required(
            new PanelCladdingMatchPlanningService(keys).CreatePlan(
                Snapshot(SourceId, source),
                new[] { Snapshot(TargetId, target) })).Targets.Single();
        Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A",
            "Repair plan omitted explicit parent 1A=0A.");
        Require(plan.UserTextDeletes.Contains(
                PanelCladdingKeyService.GetCellKey(1, "B"),
                StringComparer.OrdinalIgnoreCase) &&
                plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "B")] == "0B",
            "Repair plan did not replace stale 1B with its mapped parent.");

        IReadOnlyDictionary<string, string> applied =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(target, plan);
        Require(PanelCladdingMatchPlanningService.ValidateAppliedUserTextPlan(applied, plan).Success,
            "Correctly applied repair plan failed postcondition validation.");
        Require(applied[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] == "100" &&
                applied[PanelCladdingKeyService.GetVerticalOffsetKey(0)] == "60" &&
                applied[PanelCladdingKeyService.SegmentMaskKey] == targetMasks.SegmentMask &&
                applied[PanelCladdingKeyService.MergeMaskKey] == targetMasks.MergeMask &&
                applied[PanelCladdingKeyService.TypeCodeKey] == "TARGET-TYPE" &&
                !applied.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                applied["CustomNote"] == "preserve",
            "Repair changed target-owned non-cell attributes or retained Signature.");

        var missingParent = new Dictionary<string, string>(applied, StringComparer.OrdinalIgnoreCase);
        missingParent.Remove(PanelCladdingKeyService.GetCellKey(1, "A"));
        OperationResponse missingParentValidation =
            PanelCladdingMatchPlanningService.ValidateAppliedUserTextPlan(missingParent, plan);
        Require(!missingParentValidation.Success && missingParentValidation.Message.Contains(
                "WRITE_MISMATCH",
                StringComparison.Ordinal),
            "Postcondition validation accepted a missing parent value.");

        var staleHidden = new Dictionary<string, string>(applied, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "OLD-003"
        };
        OperationResponse staleHiddenValidation =
            PanelCladdingMatchPlanningService.ValidateAppliedUserTextPlan(staleHidden, plan);
        Require(!staleHiddenValidation.Success && staleHiddenValidation.Message.Contains(
                "WRITE_MISMATCH",
                StringComparison.Ordinal),
            "Postcondition validation accepted a stale value over a mapped parent write.");

        Console.WriteLine("[OK] populated target is repaired with 1A=0A and mapped 1B=0B");
        Console.WriteLine("[OK] target-owned non-cell attributes remain unchanged");
        Console.WriteLine("[OK] postcondition validation rejects missing writes and retained deletes");
    }

    private static IReadOnlyDictionary<string, string> GridText(
        PanelCladdingTopologyPayloads masks,
        string horizontal,
        string vertical) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = horizontal,
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = vertical,
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyDictionary<string, string> userText) => new()
        {
            ObjectId = objectId,
            Geometry = new PanelCladdingMatchGeometryDescriptor
            {
                GeometryClass = PanelGeometryClass.Planar,
                Width = 120d,
                Height = 200d,
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
