using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PCMatchSrfExplicitParentCellsSmoke;

internal static class Program
{
    private static readonly Guid SourceId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetId =
        Guid.Parse("72000000-0000-0000-0000-000000000001");

    private static void Main()
    {
        VerifyReportedTwoByFourParentGraph();
        Console.WriteLine("[OK] reported 2x4 graph copies 1A=0A through 1D=0D exactly");

        VerifyExplicitParentSurvivesTopologyCollapse();
        Console.WriteLine("[OK] explicit parent survives collapse and target-visible cells inherit the region");
    }

    private static void VerifyReportedTwoByFourParentGraph()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyPayloads sourceMasks = Masks(
            keys,
            new PanelCladdingTopologyState());
        var source = new Dictionary<string, string>(
            GridText(sourceMasks, h0: "152.20455", h1: "163.875", h2: "204.625", v0: "45"),
            StringComparer.OrdinalIgnoreCase);
        string[] ownerMaterials = { "MPL-001", "MPL-001", "MPL-002", "MPL-001" };
        for (int row = 0; row < 4; row++)
        {
            string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
            source[PanelCladdingKeyService.GetCellKey(0, rowLabel)] = ownerMaterials[row];
            source[PanelCladdingKeyService.GetCellKey(1, rowLabel)] = $"0{rowLabel}";
        }

        var target = new Dictionary<string, string>(
            GridText(Masks(keys, new PanelCladdingTopologyState()), "10", "20", "30", "60"),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.TypeCodeKey] = "TARGET-TYPE",
            [PanelCladdingKeyService.SignatureKey] = "target-signature",
            ["CustomNote"] = "preserve"
        };

        PanelCladdingMatchTargetPlan plan = Required(
            new PanelCladdingMatchPlanningService(keys).CreatePlan(
                Snapshot(SourceId, source),
                new[] { Snapshot(TargetId, target) })).Targets.Single();
        for (int row = 0; row < 4; row++)
        {
            string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
            Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(0, rowLabel)] ==
                    ownerMaterials[row],
                $"Owner 0{rowLabel} was not copied.");
            Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, rowLabel)] ==
                    $"0{rowLabel}",
                $"Parent 1{rowLabel}=0{rowLabel} was not copied.");
        }
        Require(plan.UserTextWrites.Count == 9 &&
                plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.CladdingLogicKey),
            "The complete 2x4 source graph must produce eight cell writes plus derived logic.");

        IReadOnlyDictionary<string, string> applied =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(target, plan);
        Require(applied[PanelCladdingKeyService.GetVerticalOffsetKey(0)] == "60" &&
                applied[PanelCladdingKeyService.SegmentMaskKey] == target[PanelCladdingKeyService.SegmentMaskKey] &&
                applied[PanelCladdingKeyService.MergeMaskKey] == target[PanelCladdingKeyService.MergeMaskKey] &&
                applied[PanelCladdingKeyService.TypeCodeKey] == "TARGET-TYPE" &&
                !applied.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                applied["CustomNote"] == "preserve",
            "PCMatchSrf changed target-owned non-cell attributes or retained Signature.");
    }

    private static void VerifyExplicitParentSurvivesTopologyCollapse()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyPayloads masks = Masks(keys, new PanelCladdingTopologyState
        {
            MissingSegments = new[]
            {
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 0),
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 1)
            }
        });
        var source = new Dictionary<string, string>(GridText(masks), StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "0A",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-001"
        };
        PanelCladdingMatchTargetPlan plan = Required(
            new PanelCladdingMatchPlanningService(keys).CreatePlan(
                Snapshot(SourceId, source),
                new[]
                {
                    Snapshot(TargetId, GridText(Masks(keys, new PanelCladdingTopologyState())))
                })).Targets.Single();

        Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "A")] == "0A",
            "An explicit source parent was discarded by topology collapse.");
        Require(plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "B")] == "0B",
            $"A target-visible cell did not inherit its source-collapsed region: " +
            plan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "B")]);
    }

    private static IReadOnlyDictionary<string, string> GridText(
        PanelCladdingTopologyPayloads masks,
        string h0 = "40",
        string h1 = "80",
        string h2 = "120",
        string v0 = "45") =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = h0,
            [PanelCladdingKeyService.GetHorizontalOffsetKey(1)] = h1,
            [PanelCladdingKeyService.GetHorizontalOffsetKey(2)] = h2,
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = v0,
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask
        };

    private static PanelCladdingTopologyPayloads Masks(
        PanelCladdingKeyService keys,
        PanelCladdingTopologyState topology) =>
        Required(keys.EncodeTopology(topology, horizontalTrackCount: 3, verticalTrackCount: 1));

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyDictionary<string, string> userText) => new()
        {
            ObjectId = objectId,
            Geometry = new PanelCladdingMatchGeometryDescriptor
            {
                GeometryClass = PanelGeometryClass.Planar,
                Width = 90d,
                Height = 279.125d,
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
