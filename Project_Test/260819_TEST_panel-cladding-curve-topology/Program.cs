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
        VerifyCommandContract();
        Console.WriteLine("[OK] PCCreate infers closed atomic segments and logical cells from on-panel guides.");
        Console.WriteLine("[OK] Continuous and split guides default to segmented curves; a 2H/2V grid emits twelve intermediate segments.");
        Console.WriteLine("[OK] PCCreate rejects endpoint-only and cascading dangling guides.");
        Console.WriteLine("[OK] PCMatchCrv transfers only masks across equal H/V counts.");
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
                targetPlan.UserTextWrites[PanelCladdingKeyService.SegmentMaskKey] == sourceMasks.SegmentMask,
            "PCMatchCrv did not write only the source's nondefault segment mask.");
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
            "PCMatchCrv is not routed through the mask-only planner.");
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
        double height) => new()
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
