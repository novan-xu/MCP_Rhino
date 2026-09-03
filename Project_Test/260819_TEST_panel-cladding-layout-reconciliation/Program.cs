using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingLayoutReconciliationSmoke;

internal static class Program
{
    private static readonly Guid SourceId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid TargetId =
        Guid.Parse("81000000-0000-0000-0000-000000000002");

    private static void Main()
    {
        VerifyGeneralInsertionRemapsTopology();
        Console.WriteLine("[OK] ordered insertion remaps retained tracks and expands only the divided bay");

        VerifySimultaneousAxisChangesAndMovement();
        Console.WriteLine("[OK] the same reconciliation handles multiple H/V insertions and moved retained tracks");

        VerifySemanticMatchAcrossDifferentRawGrids();
        Console.WriteLine("[OK] semantic matching collapses redundant source tracks and absorbs extra target tracks");

        VerifyMissingDividerRejectsRequiredBoundary();
        Console.WriteLine("[OK] a target missing a required cladding boundary is rejected before mutation");

        VerifySignatureKeysAreRetiredFromMatchPlans();
        Console.WriteLine("[OK] touched match targets remove canonical and legacy Signature keys");

        VerifyNoFixtureSpecificProductionBranch();
        Console.WriteLine("[OK] production reconciliation contains no fixture coordinates, states, or literal index transition");
    }

    private static void VerifyGeneralInsertionRemapsTopology()
    {
        var service = new PanelCladdingLayoutReconciliationService();
        var oldTopology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ],
            HiddenSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 2, 3)
            ],
            MergeRuns =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 1, 0, 3),
                new(PanelCladdingTopologyAxis.Vertical, 0, 0, 4),
                new(PanelCladdingTopologyAxis.Vertical, 1, 0, 4),
                new(PanelCladdingTopologyAxis.Vertical, 2, 0, 4)
            ]
        };
        PanelCladdingLayoutReconciliationResult result = Required(service.Reconcile(
            oldHorizontalOffsets: [10d, 30d, 50d, 70d],
            oldVerticalOffsets: [25.0729d, 42.80245d, 70.65855d],
            newHorizontalOffsets: [10d, 30d, 50d, 70d],
            newVerticalOffsets: [25.0729d, 33.93768d, 42.80245d, 70.65855d],
            oldTopology,
            panelWidth: 98.69485d,
            panelHeight: 91.10313d,
            modelTolerance: 0.001d));

        Require(result.Vertical.OldToNewTracks.SequenceEqual(
                new int?[] { 0, 2, 3 }),
            "Retained V tracks were not reindexed by identity.");
        Require(result.Vertical.NewToOldTracks.SequenceEqual(
                new int?[] { 0, null, 1, 2 }),
            "Inserted V1 was not isolated from retained tracks.");
        Require(result.Topology.MissingSegments.Contains(
                new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Horizontal, 0, 1)) &&
                result.Topology.MissingSegments.Contains(
                    new PanelCladdingSegmentCoordinate(
                        PanelCladdingTopologyAxis.Horizontal, 0, 2)),
            "The old missing horizontal bay was not expanded across its two children.");
        Require(result.Topology.MergeRuns.Contains(
                new PanelCladdingMergeRun(
                    PanelCladdingTopologyAxis.Horizontal, 1, 0, 4)),
            "A retained horizontal merged curve did not expand across the inserted divider.");
        foreach (int track in new[] { 0, 2, 3 })
        {
            Require(result.Topology.MergeRuns.Contains(
                    new PanelCladdingMergeRun(
                        PanelCladdingTopologyAxis.Vertical, track, 0, 4)),
                $"Retained vertical track V{track} lost its merge run.");
        }
        Require(!result.Topology.MergeRuns.Any(run =>
                run.Axis == PanelCladdingTopologyAxis.Vertical && run.Track == 1),
            "The inserted track inherited an unrelated old merge run.");
        Require(result.Topology.HiddenSegments.Contains(
                new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Horizontal, 2, 4)),
            "Hidden state did not follow its retained track and shifted bay.");

        PanelCladdingTopologyState refined =
            PanelCladdingLayoutReconciliationService.ApplyInsertedTrackEvidence(
                result,
                result.Topology,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["1A"] = "LEFT-A", ["2A"] = "RIGHT-A",
                    ["1B"] = "SPAN-B", ["2B"] = "SPAN-B",
                    ["1C"] = "SPAN-C", ["2C"] = "SPAN-C",
                    ["1D"] = "SPAN-D", ["2D"] = "SPAN-D",
                    ["1E"] = "SPAN-E", ["2E"] = "SPAN-E"
                },
                curveTopology: null,
                horizontalTrackCount: 4,
                verticalTrackCount: 4);
        Require(!refined.MissingSegments.Contains(
                new PanelCladdingSegmentCoordinate(
                    PanelCladdingTopologyAxis.Vertical, 1, 0)) &&
                Enumerable.Range(1, 4).All(bay => refined.MissingSegments.Contains(
                    new PanelCladdingSegmentCoordinate(
                        PanelCladdingTopologyAxis.Vertical, 1, bay))),
            "Surface ownership did not infer the inserted track's partial 2.05 segment state.");

        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet baseKeySet = Required(keys.CreateKeySet(
            [10d, 30d, 50d, 70d],
            [25.0729d, 33.93768d, 42.80245d, 70.65855d],
            new Dictionary<string, string>(),
            98.69485d,
            91.10313d,
            0.001d));
        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_GENERAL",
                "CID_GENERAL",
                98.69485d,
                91.10313d,
                new PanelCladdingKeySet
                {
                    HorizontalOffsets = baseKeySet.HorizontalOffsets,
                    VerticalOffsets = baseKeySet.VerticalOffsets,
                    Cells = baseKeySet.Cells,
                    Topology = refined
                },
                "01_CW Panels::Surfaces-PNL::WT-01"));
        string[] mergedVerticalCodes = curves
            .Where(curve => curve.Axis == PanelCladdingTopologyAxis.Vertical &&
                curve.Kind == PanelCladdingExtrusionCurveKind.Merged)
            .Select(curve => curve.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        Require(mergedVerticalCodes.SequenceEqual(new[] { "INT_0", "INT_2", "INT_3" }),
            $"Retained full-height merged curves did not reindex deterministically: " +
            string.Join(",", mergedVerticalCodes));
    }

    private static void VerifySimultaneousAxisChangesAndMovement()
    {
        PanelCladdingInferredOffsets effective = Required(
            PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                existingHorizontalOffsets: [20d, 50d],
                existingVerticalOffsets: [30d],
                surfaceInferred: new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [22d, 35d, 55d],
                    VerticalOffsets = [10d, 32d, 70d]
                },
                curveInferred: new PanelCladdingInferredOffsets
                {
                    HorizontalOffsets = [22d, 55d],
                    VerticalOffsets = [32d]
                },
                panelWidth: 100d,
                panelHeight: 80d,
                modelTolerance: 0.001d));
        Require(effective.HorizontalOffsets.SequenceEqual(new[] { 22d, 35d, 55d }) &&
                effective.VerticalOffsets.SequenceEqual(new[] { 10d, 32d, 70d }),
            "Surface-scope evidence did not distinguish moved structural tracks from insertions.");

        var service = new PanelCladdingLayoutReconciliationService();
        PanelCladdingLayoutReconciliationResult result = Required(service.Reconcile(
            oldHorizontalOffsets: [20d, 50d],
            oldVerticalOffsets: [30d],
            newHorizontalOffsets: [22d, 35d, 55d],
            newVerticalOffsets: [10d, 32d, 70d],
            oldTopology: new PanelCladdingTopologyState
            {
                MergeRuns =
                [
                    new(PanelCladdingTopologyAxis.Vertical, 0, 0, 2)
                ]
            },
            panelWidth: 100d,
            panelHeight: 80d,
            modelTolerance: 0.001d));

        Require(result.Horizontal.OldToNewTracks.SequenceEqual(new int?[] { 0, 2 }),
            "Moved H tracks did not retain ordered identity around an insertion.");
        Require(result.Vertical.OldToNewTracks.SequenceEqual(new int?[] { 1 }),
            "Moved V track did not retain identity around two insertions.");
        Require(result.Topology.MergeRuns.Contains(
                new PanelCladdingMergeRun(
                    PanelCladdingTopologyAxis.Vertical, 1, 0, 3)),
            "Moved retained curve did not preserve and expand its merge run.");
    }

    private static void VerifySemanticMatchAcrossDifferentRawGrids()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        IReadOnlyDictionary<string, string> source = GridText(
            horizontalOffsets: [30d],
            verticalOffsets: [20d, 55d],
            width: 90d,
            height: 70d,
            values: new Dictionary<string, string>
            {
                ["0A"] = "MAT-A",
                ["0B"] = "0A",
                ["1A"] = "MAT-B",
                ["1B"] = "1A",
                ["2A"] = "1A",
                ["2B"] = "1A"
            });
        IReadOnlyDictionary<string, string> fewerTarget = GridText(
            horizontalOffsets: Array.Empty<double>(),
            verticalOffsets: [45d],
            width: 120d,
            height: 80d,
            values: new Dictionary<string, string>());
        PanelCladdingMatchTargetPlan fewerPlan = Required(planner.CreatePlan(
            Snapshot(SourceId, source, 90d, 70d),
            [Snapshot(TargetId, fewerTarget, 120d, 80d)])).Targets.Single();
        Require(fewerPlan.UserTextWrites[PanelCladdingKeyService.GetCellKey(0, "A")] == "MAT-A" &&
                fewerPlan.UserTextWrites[PanelCladdingKeyService.GetCellKey(1, "A")] == "MAT-B",
            "Redundant source rows/columns were not collapsed onto the smaller target grid.");

        IReadOnlyDictionary<string, string> extraTarget = GridText(
            horizontalOffsets: Array.Empty<double>(),
            verticalOffsets: [30d, 70d],
            width: 120d,
            height: 80d,
            values: new Dictionary<string, string>());
        PanelCladdingMatchTargetPlan extraPlan = Required(planner.CreatePlan(
            Snapshot(SourceId, source, 90d, 70d),
            [Snapshot(TargetId, extraTarget, 120d, 80d)])).Targets.Single();
        string[] values = extraPlan.UserTextWrites
            .Where(item => keys.IsCladdingCellKey(item.Key))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Value)
            .ToArray();
        Require(values.SequenceEqual(new[] { "MAT-A", "MAT-B", "1A" }),
            "The deterministic extra-track mapping did not retain two logical regions.");
    }

    private static void VerifyMissingDividerRejectsRequiredBoundary()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        IReadOnlyDictionary<string, string> source = GridText(
            horizontalOffsets: Array.Empty<double>(),
            verticalOffsets: [50d],
            width: 100d,
            height: 60d,
            values: new Dictionary<string, string>
            {
                ["0A"] = "MAT-A",
                ["1A"] = "MAT-B"
            });
        PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState
            {
                MissingSegments =
                [
                    new(PanelCladdingTopologyAxis.Vertical, 0, 0)
                ]
            },
            0,
            1));
        var target = new Dictionary<string, string>(GridText(
            horizontalOffsets: Array.Empty<double>(),
            verticalOffsets: [35d],
            width: 80d,
            height: 70d,
            values: new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = masks.HideMask
        };
        OperationResponse<PanelCladdingMatchPlan> rejected = planner.CreatePlan(
            Snapshot(SourceId, source, 100d, 60d),
            [Snapshot(TargetId, target, 80d, 70d)]);
        Require(!rejected.Success && rejected.Message.Contains(
                "PANEL_CLADDING_MATCH_TARGET_PARTITION_NOT_REPRESENTABLE",
                StringComparison.Ordinal),
            $"Missing required divider did not reject: {rejected.Message}");
    }

    private static void VerifySignatureKeysAreRetiredFromMatchPlans()
    {
        var keys = new PanelCladdingKeyService();
        var planner = new PanelCladdingMatchPlanningService(keys);
        IReadOnlyDictionary<string, string> source = GridText(
            Array.Empty<double>(),
            Array.Empty<double>(),
            100d,
            100d,
            new Dictionary<string, string> { ["0A"] = "MAT-A" });
        var target = new Dictionary<string, string>(GridText(
            Array.Empty<double>(),
            Array.Empty<double>(),
            80d,
            120d,
            new Dictionary<string, string>()),
            StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:old",
            [PanelCladdingKeyService.LegacySignatureKey] = "legacy"
        };
        PanelCladdingMatchTargetPlan plan = Required(planner.CreatePlan(
            Snapshot(SourceId, source, 100d, 100d),
            [Snapshot(TargetId, target, 80d, 120d)])).Targets.Single();
        Require(plan.UserTextDeletes.Contains(
                PanelCladdingKeyService.SignatureKey,
                StringComparer.OrdinalIgnoreCase) &&
                plan.UserTextDeletes.Contains(
                    PanelCladdingKeyService.LegacySignatureKey,
                    StringComparer.OrdinalIgnoreCase) &&
                !plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.SignatureKey),
            "Match planning did not retire both signature keys.");

        string root = FindRepositoryRoot();
        string persistenceSource = string.Join("\n", new[]
        {
            Path.Combine(root, "src", "PanelCladdingEditor", "Application", "Services",
                "PanelCladdingSaveService.cs"),
            Path.Combine(root, "src", "PanelCladdingEditor", "Infrastructure", "Rhino", "Live",
                "PanelCladding", "LivePanelCladdingSurfaceSyncRepository.cs")
        }.Select(File.ReadAllText));
        Require(!persistenceSource.Contains(
                "writes[PanelCladdingKeyService.SignatureKey]",
                StringComparison.Ordinal) &&
                !persistenceSource.Contains(
                    "SetUserString(PanelCladdingKeyService.SignatureKey",
                    StringComparison.Ordinal),
            "A panel save or sync path still produces a Signature user-text value.");
    }

    private static void VerifyNoFixtureSpecificProductionBranch()
    {
        string root = FindRepositoryRoot();
        string[] productionFiles =
        [
            Path.Combine(root, "src", "PanelCladdingEditor", "Application", "Services",
                "PanelCladding", "PanelCladdingLayoutReconciliationService.cs"),
            Path.Combine(root, "src", "PanelCladdingEditor", "Application", "Services",
                "PanelCladding", "PanelCladdingMatchFeasibilityService.cs")
        ];
        string production = string.Join("\n", productionFiles.Select(File.ReadAllText));
        foreach (string forbidden in new[]
        {
            "25.0729", "33.93768", "42.80245", "70.65855",
            "state=1", "state=2", "INT_1", "INT_2", "Untitled 1.3dm"
        })
        {
            Require(!production.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Production code contains fixture-specific token '{forbidden}'.");
        }
    }

    private static IReadOnlyDictionary<string, string> GridText(
        IReadOnlyList<double> horizontalOffsets,
        IReadOnlyList<double> verticalOffsets,
        double width,
        double height,
        IReadOnlyDictionary<string, string> values)
    {
        var keys = new PanelCladdingKeyService();
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < horizontalOffsets.Count; index++)
        {
            text[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(horizontalOffsets[index]);
        }
        for (int index = 0; index < verticalOffsets.Count; index++)
        {
            text[PanelCladdingKeyService.GetVerticalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(verticalOffsets[index]);
        }
        foreach ((string label, string value) in values)
        {
            int rowStart = label.TakeWhile(char.IsDigit).Count();
            int column = int.Parse(label[..rowStart]);
            string row = label[rowStart..];
            text[PanelCladdingKeyService.GetCellKey(column, row)] = value;
        }
        PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(
            new PanelCladdingTopologyState(),
            horizontalOffsets.Count,
            verticalOffsets.Count));
        text[PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask;
        text[PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask;
        text[PanelCladdingKeyService.HideMaskKey] = masks.HideMask;
        text[PanelCladdingKeyService.UnitWidthKey] =
            PanelCladdingKeyService.FormatUnitDimension(width);
        text[PanelCladdingKeyService.UnitHeightKey] =
            PanelCladdingKeyService.FormatUnitDimension(height);
        return text;
    }

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid objectId,
        IReadOnlyDictionary<string, string> text,
        double width,
        double height) => new()
        {
            ObjectId = objectId,
            Geometry = new PanelCladdingMatchGeometryDescriptor
            {
                GeometryClass = PanelGeometryClass.Planar,
                Width = width,
                Height = height,
                ModelTolerance = 0.001d
            },
            UserText = text
        };

    private static T Required<T>(OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
