using System.Reflection;
using System.IO;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingCurveTopologySmoke;

internal static class Program
{
    private static readonly Guid SourceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static void Main()
    {
        VerifyContinuousGridSpawnsSegments();
        VerifyScreenshotTopology();
        VerifyDanglingAndCascadingGuidesReject();
        VerifySeparateCurvesDoNotInventMerge();
        VerifyCurveMaskMatch();
        VerifyCurveAssignmentMatch();
        VerifyCommandContract();
        Console.WriteLine("[OK] PCCreate infers closed atomic segments and logical cells from on-panel guides.");
        Console.WriteLine("[OK] Continuous and split guides default to segmented curves; a 2H/2V grid emits twelve intermediate segments.");
        Console.WriteLine("[OK] PCCreate rejects endpoint-only and cascading dangling guides.");
        Console.WriteLine("[OK] PCMatchCrv transfers sparse masks and full extrusion assignments across equal H/V counts.");
    }

    private static void VerifyContinuousGridSpawnsSegments()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingCreatePanelPlan plan = Required(
            new PanelCladdingCreatePlanningService(keys).CreatePlan(
                [Snapshot(new[]
                {
                    Guide("40000000-0000-0000-0000-000000000001", (0d, 50d), (100d, 50d)),
                    Guide("40000000-0000-0000-0000-000000000002", (0d, 150d), (100d, 150d)),
                    Guide("40000000-0000-0000-0000-000000000003", (25d, 0d), (25d, 200d)),
                    Guide("40000000-0000-0000-0000-000000000004", (75d, 0d), (75d, 200d))
                })])).Panels.Single();
        Require(!plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.MergeMaskKey),
            "Continuous PCCreate guides must default to segmented tracks without a merge mask.");
        PanelCladdingKeySet parsed = Required(keys.Parse(plan.UserTextWrites, 100d, 200d, 0.001d));
        Require(parsed.Topology.MergeRuns.Count == 0 && parsed.Topology.MissingSegments.Count == 0 &&
                parsed.Topology.HiddenSegments.Count == 0 && parsed.Cells.Count == 9,
            "A full 2H/2V grid must round-trip as nine cells with all intermediate atoms present, segmented, and visible.");
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PANEL-01", "CID-01", 100d, 200d, parsed,
                PanelCladdingExtrusionPlanningService.PanelSurfaceLayerRootPath + "::TEST", "01"));
        PanelCladdingExtrusionCurvePlan[] segments = curves
            .Where(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Segment).ToArray();
        Require(curves.Count == 16 && segments.Length == 12 &&
                curves.Count(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Frame) == 4 &&
                curves.All(curve => curve.Kind != PanelCladdingExtrusionCurveKind.Merged) &&
                segments.All(curve => curve.AtomicSegments.Count == 1) &&
                segments.SelectMany(curve => curve.AtomicSegments).Distinct().Count() == 12,
            "PCCreate attributes must produce twelve separate intermediate curves and four unchanged perimeter frames.");
    }

    private static void VerifyScreenshotTopology()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingCreatePlanningService(keys);
        PanelCladdingCreatePanelSnapshot snapshot = Snapshot(
            new[]
            {
                Guide("10000000-0000-0000-0000-000000000001", (50d, 0d), (50d, 200d)),
                Guide("10000000-0000-0000-0000-000000000002", (20d, 150d), (100d, 150d)),
                Guide("10000000-0000-0000-0000-000000000003", (50d, 100d), (100d, 100d)),
                Guide("10000000-0000-0000-0000-000000000004", (0d, 60d), (60d, 60d)),
                Guide("10000000-0000-0000-0000-000000000005", (-20d, 30d), (0d, 30d), false)
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingKeyService.SegmentMaskKey] = "stale-segment",
                [PanelCladdingKeyService.MergeMaskKey] = "stale-merge",
                [PanelCladdingKeyService.HideMaskKey] = "stale-hide",
                ["CustomNote"] = "preserve"
            });
        PanelCladdingCreatePanelPlan plan = Required(planner.CreatePlan([snapshot])).Panels.Single();

        Require(plan.HorizontalOffsets.SequenceEqual(new[] { 60d, 100d, 150d }) &&
                plan.VerticalOffsets.SequenceEqual(new[] { 50d }),
            "Screenshot guide offsets were not inferred bottom-up/left-right.");
        PanelCladdingTopologyState topology = Required(keys.DecodeTopology(
            plan.UserTextWrites,
            horizontalTrackCount: 3,
            verticalTrackCount: 1));
        var expectedMissing = new HashSet<PanelCladdingSegmentCoordinate>
        {
            new(PanelCladdingTopologyAxis.Horizontal, 0, 1),
            new(PanelCladdingTopologyAxis.Horizontal, 1, 0),
            new(PanelCladdingTopologyAxis.Horizontal, 2, 0)
        };
        Require(topology.MissingSegments.ToHashSet().SetEquals(expectedMissing),
            "Screenshot guide spans did not produce the expected three absent horizontal atoms.");
        Require(topology.MergeRuns.Count == 0 &&
                !plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.MergeMaskKey),
            "The continuous full-height vertical guide must remain segmented around partial horizontal tracks.");
        Require(plan.UserTextWrites.Keys.Count(keys.IsCladdingCellKey) == 5,
            "The screenshot topology must persist five surviving logical blank cells.");
        Require(plan.UserTextDeletes.Contains(PanelCladdingKeyService.SegmentMaskKey, StringComparer.OrdinalIgnoreCase) &&
                plan.UserTextDeletes.Contains(PanelCladdingKeyService.MergeMaskKey, StringComparer.OrdinalIgnoreCase) &&
                plan.UserTextDeletes.Contains(PanelCladdingKeyService.HideMaskKey, StringComparer.OrdinalIgnoreCase) &&
                !plan.UserTextDeletes.Contains("CustomNote", StringComparer.OrdinalIgnoreCase),
            "PCCreate did not reset stale masks while preserving unrelated metadata.");
        Require(plan.Warnings.Count == 1 &&
                plan.Warnings[0].Contains("GUIDE_NOT_ON_PANEL", StringComparison.Ordinal),
            "The endpoint-only guide was not reported and ignored.");
    }

    private static void VerifyDanglingAndCascadingGuidesReject()
    {
        var planner = new PanelCladdingCreatePlanningService(new PanelCladdingKeyService());
        OperationResponse<PanelCladdingCreatePlan> endpointOnly = planner.CreatePlan(
            [Snapshot([Guide("20000000-0000-0000-0000-000000000001", (-10d, 40d), (0d, 40d), false)])]);
        RequireFailure(endpointOnly, "APPLICABLE_GUIDES_REQUIRED");

        OperationResponse<PanelCladdingCreatePlan> cascading = planner.CreatePlan(
            [Snapshot(new[]
            {
                Guide("20000000-0000-0000-0000-000000000002", (0d, 50d), (40d, 50d)),
                Guide("20000000-0000-0000-0000-000000000003", (40d, 10d), (40d, 40d))
            })]);
        RequireFailure(cascading, "APPLICABLE_GUIDES_REQUIRED");
    }

    private static void VerifySeparateCurvesDoNotInventMerge()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingCreatePanelPlan plan = Required(
            new PanelCladdingCreatePlanningService(keys).CreatePlan(
                [Snapshot(new[]
                {
                    Guide("30000000-0000-0000-0000-000000000001", (50d, 0d), (50d, 200d)),
                    Guide("30000000-0000-0000-0000-000000000002", (0d, 100d), (50d, 100d)),
                    Guide("30000000-0000-0000-0000-000000000003", (50d, 100d), (100d, 100d))
                })])).Panels.Single();
        PanelCladdingTopologyState topology = Required(keys.DecodeTopology(plan.UserTextWrites, 1, 1));
        Require(topology.MergeRuns.Count == 0 &&
                !plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.MergeMaskKey),
            "Separate horizontal guides and a continuous vertical guide must all start segmented.");
    }

    private static void VerifyCurveMaskMatch()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyState sourceTopology = new()
        {
            MissingSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1),
                new(PanelCladdingTopologyAxis.Vertical, 0, 1)
            ]
        };
        PanelCladdingTopologyPayloads sourceMasks = Required(keys.EncodeTopology(sourceTopology, 2, 1));
        PanelCladdingTopologyPayloads targetMasks = Required(keys.EncodeTopology(new PanelCladdingTopologyState(), 2, 1));
        var sourceText = new Dictionary<string, string>(Grid("30", "70", "50", sourceMasks),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001"
        };
        var targetText = new Dictionary<string, string>(Grid("20", "160", "65", targetMasks),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-001",
            [PanelCladdingKeyService.TypeCodeKey] = "TARGET-TYPE",
            ["CustomNote"] = "preserve"
        };
        PanelCladdingMatchTargetPlan targetPlan = Required(
            new PanelCladdingCurveMatchPlanningService(keys).CreatePlan(
                MatchSnapshot(SourceId, sourceText, 100d, 100d),
                [MatchSnapshot(TargetId, targetText, 180d, 240d)])).Targets.Single();
        Require(targetPlan.UserTextWrites.Count == 1 &&
                targetPlan.UserTextWrites[PanelCladdingKeyService.FrameConfigKey] ==
                    Required(keys.EncodeFrameConfiguration(sourceTopology, 2, 1)),
            "PCMatchCrv did not write the source's combined frame configuration.");
        Require(targetPlan.UserTextDeletes.All(PanelCladdingKeyService.IsTopologyKey),
            "PCMatchCrv planned a non-topology deletion.");
        IReadOnlyDictionary<string, string> applied =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(targetText, targetPlan);
        Require(applied[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] == "20" &&
                applied[PanelCladdingKeyService.GetHorizontalOffsetKey(1)] == "160" &&
                applied[PanelCladdingKeyService.GetVerticalOffsetKey(0)] == "65" &&
                applied[PanelCladdingKeyService.GetCellKey(0, "A")] == "GLS-001" &&
                applied[PanelCladdingKeyService.TypeCodeKey] == "TARGET-TYPE" &&
                applied["CustomNote"] == "preserve",
            "PCMatchCrv changed target offsets, cells, type metadata, or unrelated text.");
        Require(Required(keys.DecodeTopology(applied, 2, 1)).MissingSegments.ToHashSet()
                .SetEquals(sourceTopology.MissingSegments),
            "PCMatchCrv did not apply source topology to different target offset values.");

        OperationResponse<PanelCladdingMatchPlan> mismatch =
            new PanelCladdingCurveMatchPlanningService(keys).CreatePlan(
                MatchSnapshot(SourceId, sourceText, 100d, 100d),
                [MatchSnapshot(TargetId, new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "30",
                    [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "50"
                }, 100d, 100d)]);
        RequireFailure(mismatch, "LAYOUT_MISMATCH");

        var invalidSourceText = new Dictionary<string, string>(sourceText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SegmentMaskKey] = "not-a-topology-payload"
        };
        OperationResponse<PanelCladdingMatchPlan> invalidSource =
            new PanelCladdingCurveMatchPlanningService(keys).CreatePlan(
                MatchSnapshot(SourceId, invalidSourceText, 100d, 100d),
                [MatchSnapshot(TargetId, targetText, 180d, 240d)]);
        RequireFailure(invalidSource, "SOURCE_INVALID");
    }

    private static void VerifyCurveAssignmentMatch()
    {
        var keys = new PanelCladdingKeyService();
        var assignments = new PanelFrameAssignmentService();
        var planner = new PanelCladdingCurveMatchPlanningService(keys);
        var h0 = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 0);
        var h1 = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1);
        var v0 = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 0);
        var hidden = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 2);
        var missing = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 1, 1);
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments = [missing],
            HiddenSegments = [hidden],
            MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
        };
        var state = new PanelFrameAssignmentState
        {
            FrameAssignments = new Dictionary<string, IReadOnlyList<string>>
            {
                ["FRM_0"] = ["1D-H0579", "0D-FIX", "0D-SPACED"],
                ["FRM_3"] = ["1D-H0579"]
            },
            SegmentAssignments =
            [
                new(h0, ["1D-H0579"]), new(h1, ["1D-H0579"]),
                new(v0, ["0D-SPACED"]), new(hidden, ["0D-FIX"])
            ],
            Definitions = new Dictionary<string, PanelFrameProfileDefinition>
            {
                ["1D-H0579"] = new()
                {
                    Code = "1D-H0579", BaseCode = "H0579", SourceCode = "ALU-H0579",
                    Category = "FRAMING", Dimension = PanelFrameProfileDimension.OneDimensional,
                    Calculation = PanelFrameProfileCalculation.Length, CalculationValue = 2d
                },
                ["0D-FIX"] = new()
                {
                    Code = "0D-FIX", BaseCode = "FIX", SourceCode = "ALU-FIX",
                    Category = "FIXING", Dimension = PanelFrameProfileDimension.ZeroDimensional,
                    Calculation = PanelFrameProfileCalculation.FixedQuantity, CalculationValue = 3d,
                    ParentCode = "1D-H0579"
                },
                ["0D-SPACED"] = new()
                {
                    Code = "0D-SPACED", BaseCode = "SPACED", SourceCode = "ALU-SPACED",
                    Category = "FIXING", Dimension = PanelFrameProfileDimension.ZeroDimensional,
                    Calculation = PanelFrameProfileCalculation.Spacing, CalculationValue = 24d
                }
            },
            CurveModifiers = new Dictionary<string, double>
            {
                ["FRM_0"] = 4d,
                [PanelFrameAssignmentService.SegmentKey(h0)] = -2d,
                [PanelFrameAssignmentService.SegmentKey(h1)] = -2d
            }
        };
        string encoded = Required(assignments.Encode(state, 2, 1, topology));
        var sourceText = new Dictionary<string, string>(Grid(
            "30", "70", "50", Required(keys.EncodeTopology(topology, 2, 1))))
        {
            [PanelCladdingKeyService.FrameTypeKey] = encoded,
            [PanelCladdingKeyService.LegacyFrameTypologyKey] = "SOURCE-STALE",
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001"
        };
        var targetText = new Dictionary<string, string>(Grid(
            "20", "160", "65", Required(keys.EncodeTopology(new(), 2, 1))))
        {
            [PanelCladdingKeyService.FrameTypeKey.ToLowerInvariant()] =
                "{\"v\":1,\"f\":{\"FRM_2\":[\"1D-OLD\"]},\"s\":[{\"a\":\"H\",\"t\":1,\"b\":1,\"c\":[\"1D-OLD\"]}]}",
            [PanelCladdingKeyService.LegacyFrameTypologyKey.ToLowerInvariant()] = "TARGET-STALE",
            [PanelCladdingKeyService.SignatureKey] = "STALE-SIGNATURE",
            [PanelCladdingKeyService.LegacySignatureKey] = "STALE-LEGACY",
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "GLS-001",
            [PanelCladdingKeyService.UnitWidthKey] = "180.00000",
            ["CW_1.01_PID"] = "TARGET-PID",
            ["CustomNote"] = "preserve"
        };
        PanelCladdingMatchPanelSnapshot source = MatchSnapshot(SourceId, sourceText, 100d, 100d, "WT-01");
        PanelCladdingMatchPanelSnapshot[] targets =
        [
            MatchSnapshot(TargetId, targetText, 180d, 240d, "WT-02"),
            MatchSnapshot(Guid.Parse("33333333-3333-3333-3333-333333333333"), targetText, 220d, 300d, "WT-03")
        ];
        PanelCladdingMatchPlan plan = Required(planner.CreatePlan(source, targets));
        var legacyFrameText = new Dictionary<string, string>(sourceText);
        legacyFrameText.Remove(PanelCladdingKeyService.FrameTypeKey);
        legacyFrameText[PanelCladdingKeyService.LegacyFrameAssignmentsKey] = encoded;
        PanelCladdingMatchTargetPlan migrated = Required(planner.CreatePlan(
            MatchSnapshot(SourceId, legacyFrameText, 100d, 100d), [targets[0]])).Targets.Single();
        Require(migrated.UserTextWrites[PanelCladdingKeyService.FrameTypeKey] == encoded &&
                migrated.UserTextWrites.ContainsKey(PanelCladdingKeyService.FrameConfigKey) &&
                !migrated.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsRetiredFrameKey),
            "PCMatchCrv must migrate an older source to the new frame keys.");
        foreach (PanelCladdingMatchTargetPlan targetPlan in plan.Targets)
        {
            PanelCladdingMatchPanelSnapshot target = targets.Single(item => item.ObjectId == targetPlan.ObjectId);
            IReadOnlyDictionary<string, string> applied = PanelCladdingMatchPlanningService.ApplyUserTextPlan(
                target.UserText, targetPlan);
            PanelCladdingKeySet parsed = Required(keys.Parse(
                applied, target.Geometry.Width, target.Geometry.Height, 0.001d));
            Require(Required(assignments.Encode(parsed.FrameAssignments, 2, 1, parsed.Topology)) == encoded,
                "PCMatchCrv lost profiles, definitions, quantities, parent links or modifiers.");
            Require(!parsed.FrameAssignments.Definitions.ContainsKey("1D-OLD") &&
                    parsed.FrameAssignments.SegmentAssignments.Any(item => item.Segment == hidden) &&
                    !parsed.FrameAssignments.SegmentAssignments.Any(item => item.Segment == missing),
                "Old assignments must be replaced atomically with source topology, including hidden assignments.");
            foreach ((string key, string value) in targetText.Where(item =>
                         !PanelCladdingKeyService.IsTopologyKey(item.Key) &&
                         !item.Key.Equals(PanelCladdingKeyService.FrameTypeKey, StringComparison.OrdinalIgnoreCase) &&
                         !item.Key.Equals(PanelCladdingKeyService.LegacyFrameTypologyKey, StringComparison.OrdinalIgnoreCase) &&
                         !item.Key.Equals(PanelCladdingKeyService.SignatureKey, StringComparison.OrdinalIgnoreCase) &&
                         !item.Key.Equals(PanelCladdingKeyService.LegacySignatureKey, StringComparison.OrdinalIgnoreCase)))
            {
                Require(applied[key] == value, $"PCMatchCrv changed target metadata: {key}.");
            }
            Require(!applied.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                    !applied.ContainsKey(PanelCladdingKeyService.LegacySignatureKey),
                "PCMatchCrv retained stale signatures.");
            Require(applied[PanelCladdingKeyService.FrameConfigKey] ==
                    Required(keys.EncodeFrameConfiguration(topology, 2, 1)) &&
                    !applied.ContainsKey(PanelCladdingKeyService.LegacyFrameTypologyKey),
                "PCMatchCrv must copy topology configuration and retire frame typology.");
            IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
                new PanelCladdingExtrusionPlanningService().CreatePlan(
                    "TARGET-PID", "TARGET-CID", target.Geometry.Width, target.Geometry.Height, parsed,
                    PanelCladdingExtrusionPlanningService.PanelSurfaceLayerRootPath + "::" + target.SystemCode));
            PanelCladdingExtrusionCurvePlan frame = curves.Single(curve => curve.Code == "FRM_0");
            PanelCladdingExtrusionCurvePlan merged = curves.Single(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Merged);
            Require(frame.End == target.Geometry.Width && frame.AssignedExtrusionCodes.Count == 3 &&
                    frame.UserTextWrites["1D-H0579"] == "(LL+4)*2" &&
                    frame.UserTextWrites["0D-FIX"] == "3" && frame.UserTextWrites["0D-SPACED"] == "(LL+4)/24" &&
                    merged.End == target.Geometry.Width && merged.UserTextWrites["1D-H0579"] == "(LL-2)*2" &&
                    curves.Single(curve => curve.AtomicSegments.Contains(v0)).UserTextWrites["0D-SPACED"] == "LL/24" &&
                    !curves.Any(curve => curve.AtomicSegments.Contains(hidden) || curve.AtomicSegments.Contains(missing)),
                "Matched assignments did not generate target-sized curves with copied formulas and suppression.");
            PanelCladdingMatchTargetPlan repeated = Required(planner.CreatePlan(source,
                [MatchSnapshot(target.ObjectId, applied, target.Geometry.Width, target.Geometry.Height, target.SystemCode)]))
                .Targets.Single();
            IReadOnlyDictionary<string, string> reapplied = PanelCladdingMatchPlanningService.ApplyUserTextPlan(applied, repeated);
            Require(applied.Count == reapplied.Count && applied.All(item => reapplied[item.Key] == item.Value),
                "Repeated PCMatchCrv changed already-matched assignments or derived metadata.");
        }
        Require(source.UserText.Count == sourceText.Count && sourceText.All(item => source.UserText[item.Key] == item.Value),
            "PCMatchCrv mutated its source.");

        var emptySourceText = new Dictionary<string, string>(sourceText);
        emptySourceText.Remove(PanelCladdingKeyService.FrameTypeKey);
        PanelCladdingMatchTargetPlan clear = Required(planner.CreatePlan(
            MatchSnapshot(SourceId, emptySourceText, 100d, 100d), [targets[0]])).Targets.Single();
        IReadOnlyDictionary<string, string> cleared = PanelCladdingMatchPlanningService.ApplyUserTextPlan(targetText, clear);
        Require(!cleared.ContainsKey(PanelCladdingKeyService.FrameTypeKey) &&
                !cleared.ContainsKey(PanelCladdingKeyService.LegacyFrameTypologyKey) &&
                Required(keys.Parse(cleared, 180d, 240d, 0.001d)).FrameAssignments.IsEmpty,
            "An unassigned source must remove case-variant target assignment and typology keys.");

        var legacySourceText = new Dictionary<string, string>(emptySourceText)
        {
            [PanelCladdingKeyService.FrameTypeKey] = "{\"v\":1,\"f\":{\"FRM_0\":[\"1D-LEGACY\"]},\"s\":[]}"
        };
        PanelCladdingMatchTargetPlan legacyPlan = Required(planner.CreatePlan(
            MatchSnapshot(SourceId, legacySourceText, 100d, 100d), [targets[0]])).Targets.Single();
        Require(Required(assignments.Decode(legacyPlan.UserTextWrites, 2, 1, topology))
                .Definitions["1D-LEGACY"].CalculationValue == 1d,
            "Legacy assignments must acquire their existing default length definition when matched.");
        foreach (string badPayload in new[]
                 {
                     "not-json",
                     "{\"v\":1,\"f\":{},\"s\":[{\"a\":\"H\",\"t\":0,\"b\":0,\"c\":[\"1D-H0579\"]}]}"
                 })
        {
            var badSource = new Dictionary<string, string>(sourceText)
            {
                [PanelCladdingKeyService.FrameTypeKey] = badPayload
            };
            RequireFailure(planner.CreatePlan(MatchSnapshot(SourceId, badSource, 100d, 100d), targets), "SOURCE_INVALID");
        }
        var badTarget = new Dictionary<string, string>(targetText)
        {
            [PanelCladdingKeyService.FrameTypeKey.ToLowerInvariant()] = "not-json"
        };
        OperationResponse<PanelCladdingMatchPlan> rejected = planner.CreatePlan(source,
            [targets[0], MatchSnapshot(targets[1].ObjectId, badTarget, 220d, 300d)]);
        RequireFailure(rejected, "TARGET_INVALID");
        Require(rejected.Data is null, "A bad later target must not return a partial match plan.");
        Console.WriteLine("[OK] PCMatchCrv copies perimeter, merged, vertical and hidden assignments; formulas generate on target-sized curves.");
        Console.WriteLine("[OK] PCMatchCrv copies frame configuration, replaces/clears stale assignments, supports legacy payloads and is idempotent.");
        Console.WriteLine("[OK] Malformed payloads and inconsistent merged assignments reject before any target plan is returned.");
    }

    private static void VerifyCommandContract()
    {
        Assembly assembly = typeof(PanelCladdingCurveMatchPlanningService).Assembly;
        Type command = assembly.GetType("PanelCladdingEditor.UI.PanelCladdingMatchCurveCommand") ??
            throw new InvalidOperationException("PCMatchCrv command type is missing.");
        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .ToArray();
        Require(typeof(Command).IsAssignableFrom(command) && command.GUID != Guid.Empty,
            "PCMatchCrv is not an explicitly identified Rhino command.");
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "PanelCladdingEditor command GUIDs are not unique.");
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PanelCladdingEditor", "UI", "PanelCladdingMatchCurveCommand.cs"));
        Require(source.Contains("EnglishName => \"PCMatchCrv\"", StringComparison.Ordinal),
            "The exact PCMatchCrv public command name is missing.");
        Require(source.Contains("new PanelCladdingCurveMatchPlanningService(keys)", StringComparison.Ordinal),
            "PCMatchCrv is not routed through the curve-match planner.");
        string geometrySource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
            "PanelCladding", "LivePanelCladdingGeometryPartitionService.cs"));
        Require(geometrySource.Contains(
                    "IsOnPanel = HasMeaningfulPanelOverlap(source, guide.Curve, local, tolerance)",
                    StringComparison.Ordinal) &&
                geometrySource.Contains("source.ClosestPoint(point)", StringComparison.Ordinal),
            "The live PCCreate snapshot does not validate meaningful guide coverage on the Brep.");
    }

    private static PanelCladdingCreatePanelSnapshot Snapshot(
        IReadOnlyList<PanelCladdingCreateGuideSnapshot> guides,
        IReadOnlyDictionary<string, string>? userText = null) => new()
    {
        ObjectId = SourceId,
        XMinimum = 0d,
        XMaximum = 100d,
        YMinimum = 0d,
        YMaximum = 200d,
        ZMinimum = -1d,
        ZMaximum = 1d,
        Tolerance = 0.001d,
        Guides = guides,
        UserText = userText ?? new Dictionary<string, string>()
    };

    private static PanelCladdingCreateGuideSnapshot Guide(
        string objectId,
        (double X, double Y) start,
        (double X, double Y) end,
        bool isOnPanel = true) => new()
    {
        ObjectId = Guid.Parse(objectId),
        IsOnPanel = isOnPanel,
        Samples =
        [
            new(start.X, start.Y, 0d),
            new((start.X + end.X) / 2d, (start.Y + end.Y) / 2d, 0d),
            new(end.X, end.Y, 0d)
        ]
    };

    private static IReadOnlyDictionary<string, string> Grid(
        string h0,
        string h1,
        string v0,
        PanelCladdingTopologyPayloads masks) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = h0,
            [PanelCladdingKeyService.GetHorizontalOffsetKey(1)] = h1,
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = v0,
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = masks.HideMask
        };

    private static PanelCladdingMatchPanelSnapshot MatchSnapshot(
        Guid id,
        IReadOnlyDictionary<string, string> userText,
        double width,
        double height,
        string systemCode = "") => new()
    {
        ObjectId = id,
        SystemCode = systemCode,
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

    private static void RequireFailure<T>(OperationResponse<T> response, string expected)
    {
        Require(!response.Success && response.Message.Contains(expected, StringComparison.Ordinal),
            $"Expected failure containing {expected}, got {response.Message}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root could not be located.");
    }
}
