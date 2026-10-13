using System.IO;
using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingHideMaskSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        HideMaskRoundTripsAndRemainsBackwardCompatible();
        HiddenSegmentsRemainLogicalButDoNotSpawn();
        HiddenTracksDoNotCollapse();
        CurveMatchCopiesAllTopologyMasks();
        EditorExposesAReversibleHiddenState();
        EditorHideDoesNotCollapseTheGrid();
        Console.WriteLine("[OK] CW_2.11_HIDE_MASK round-trips with legacy two-mask compatibility.");
        Console.WriteLine("[OK] hidden atoms retain the logical grid and are omitted from PCSpawnCrv plans.");
        Console.WriteLine("[OK] full hidden tracks do not collapse or renumber cladding cells.");
        Console.WriteLine("[OK] PCMatchCrv and PCEditor include the hide state.");
        return 0;
    }

    private static void HideMaskRoundTripsAndRemainsBackwardCompatible()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyState source = HiddenFixture();
        PanelCladdingTopologyPayloads payloads = Required(keys.EncodeTopology(source, 3, 1));
        Require(!string.IsNullOrWhiteSpace(payloads.HideMask), "Hide mask was not encoded.");
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SegmentMaskKey] = payloads.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = payloads.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = payloads.HideMask
        };
        PanelCladdingTopologyState decoded = Required(keys.DecodeTopology(text, 3, 1));
        Require(decoded.HiddenSegments.ToHashSet().SetEquals(source.HiddenSegments),
            "Encoded hide mask did not restore the selected atoms.");
        Require(decoded.MissingSegments.Count == 0, "Hidden atoms were decoded as deleted atoms.");

        text.Remove(PanelCladdingKeyService.HideMaskKey);
        PanelCladdingTopologyState legacy = Required(keys.DecodeTopology(text, 3, 1));
        Require(legacy.HiddenSegments.Count == 0,
            "A legacy segment/merge pair did not default to an empty hide mask.");
        Require(PanelCladdingKeyService.IsTopologyKey(PanelCladdingKeyService.HideMaskKey),
            "CW_2.11_HIDE_MASK is not owned by topology cleanup/persistence.");

        PanelCladdingTopologyState invalid = new()
        {
            MissingSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)],
            HiddenSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)]
        };
        Require(!keys.EncodeTopology(invalid, 1, 1).Success,
            "The same atom was accepted as both missing and hidden.");

        PanelCladdingTopologyState hiddenMerged = new()
        {
            HiddenSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ],
            MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
        };
        PanelCladdingTopologyPayloads hiddenMergedPayloads = Required(keys.EncodeTopology(hiddenMerged, 1, 1));
        PanelCladdingTopologyState hiddenMergedDecoded = Required(keys.DecodeTopology(
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.SegmentMaskKey] = hiddenMergedPayloads.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = hiddenMergedPayloads.MergeMask,
                [PanelCladdingKeyService.HideMaskKey] = hiddenMergedPayloads.HideMask
            }, 1, 1));
        Require(hiddenMergedDecoded.MergeRuns.SequenceEqual(hiddenMerged.MergeRuns),
            "A fully hidden merged curve lost its merge state.");
        Require(!keys.EncodeTopology(new PanelCladdingTopologyState
        {
            HiddenSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)],
            MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0, 1)]
        }, 1, 1).Success, "A partially hidden merged run was accepted.");
    }

    private static void HiddenSegmentsRemainLogicalButDoNotSpawn()
    {
        var keys = new PanelCladdingKeyService();
        double[] horizontal = [40d, 80d, 120d];
        double[] vertical = [45d];
        PanelCladdingKeySet baseGrid = Required(keys.CreateKeySet(
            horizontal,
            vertical,
            new Dictionary<string, string>(),
            90d,
            160d,
            0.001d));
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = baseGrid.HorizontalOffsets,
            VerticalOffsets = baseGrid.VerticalOffsets,
            Cells = baseGrid.Cells,
            Topology = HiddenFixture()
        };

        IReadOnlyList<PanelCladdingExtrusionCurvePlan> curves = Required(
            new PanelCladdingExtrusionPlanningService().CreatePlan(
                "PID_BKT_N1_01_07",
                "CID_BKT_N1_01_07",
                90d,
                160d,
                keySet,
                "01_CW Panels::Surfaces-PNL::WT-01"));
        HashSet<PanelCladdingSegmentCoordinate> plannedAtoms = curves
            .SelectMany(curve => curve.AtomicSegments)
            .ToHashSet();
        Require(!plannedAtoms.Overlaps(keySet.Topology.HiddenSegments),
            "PCSpawnCrv planned physical curves for hidden atoms.");
        Require(curves.Count(curve => curve.Kind == PanelCladdingExtrusionCurveKind.Frame) == 4,
            "Hiding intermediate atoms changed the panel frame.");

        var values = baseGrid.Cells.ToDictionary(
            cell => cell.UserTextKey,
            cell => cell.ShortLabel,
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> logical = new PanelCladdingLogicalCellService().Collapse(
            baseGrid.Cells,
            keySet.Topology,
            values);
        Require(logical.Count == 8 && baseGrid.Cells.Count == 8,
            "Hidden extrusion atoms collapsed logical cladding cells.");
    }

    private static void HiddenTracksDoNotCollapse()
    {
        var keys = new PanelCladdingKeyService();
        var hiddenTrack = new PanelCladdingTopologyState
        {
            HiddenSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ]
        };
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetCellKey(0, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(1, "A")] = "MPL-001",
            [PanelCladdingKeyService.GetCellKey(0, "B")] = "MPL-002",
            [PanelCladdingKeyService.GetCellKey(1, "B")] = "MPL-002"
        };
        PanelCladdingTopologyNormalizationResult normalized = Required(
            new PanelCladdingTopologyNormalizationService(keys).Normalize(
                [50d], [45d], values, hiddenTrack, 90d, 100d, 0.001d));
        Require(normalized.CollapsedTracks.Count == 0 &&
                normalized.HorizontalOffsets.SequenceEqual(new[] { 50d }) &&
                normalized.CellValues.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(values.Keys),
            "A fully hidden track collapsed or renumbered logical cells.");
    }

    private static void CurveMatchCopiesAllTopologyMasks()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingTopologyPayloads sourceMasks = Required(keys.EncodeTopology(HiddenFixture(), 3, 1));
        PanelCladdingTopologyPayloads targetMasks = Required(keys.EncodeTopology(new PanelCladdingTopologyState(), 3, 1));
        PanelCladdingMatchTargetPlan plan = Required(new PanelCladdingCurveMatchPlanningService(keys).CreatePlan(
            Snapshot(Guid.Parse("11111111-1111-1111-1111-111111111111"), 90d, 160d, [40d, 80d, 120d], [45d], sourceMasks),
            [Snapshot(Guid.Parse("22222222-2222-2222-2222-222222222222"), 120d, 240d, [60d, 120d, 180d], [70d], targetMasks)])).Targets.Single();
        Require(plan.UserTextWrites.Count == 1 &&
                plan.UserTextWrites.ContainsKey(PanelCladdingKeyService.FrameConfigKey) &&
                Required(keys.EncodeTopology(Required(keys.DecodeTopology(plan.UserTextWrites, 3, 1)), 3, 1)).HideMask == sourceMasks.HideMask,
            "PCMatchCrv did not copy the hide mask in combined configuration.");
    }

    private static void EditorExposesAReversibleHiddenState()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml"));
        string window = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml.cs"));
        string canvas = File.ReadAllText(Path.Combine(root, "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(xaml.Contains("x:Name=\"HideExtrusionsButton\"", StringComparison.Ordinal) &&
                xaml.Contains("Click=\"OnHideExtrusionsClick\"", StringComparison.Ordinal),
            "Extrusion view does not expose the Hide action beside Merge/Explode.");
        Require(window.Contains("_hiddenExtrusions", StringComparison.Ordinal) &&
                window.Contains("allHidden ? \"Unhide\" : \"Hide\"", StringComparison.Ordinal),
            "The editor does not retain and reverse hidden selections.");
        Require(canvas.Contains("· HIDDEN", StringComparison.Ordinal) &&
                canvas.Contains("DashStyle", StringComparison.Ordinal),
            "Hidden atoms are not visibly distinguishable/selectable in extrusion view.");
    }

    private static void EditorHideDoesNotCollapseTheGrid()
    {
        var keys = new PanelCladdingKeyService();
        double[] horizontal = [40d, 80d, 120d];
        double[] vertical = [45d];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 4; row++)
            {
                values[PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row))] =
                    row >= 2 ? "MPL-002" : "MPL-001";
            }
        }
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontal, vertical, values, 90d, 160d, 0.001d));
        var layout = new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-hide-mask.3dm",
            ObjectName = "PID_BKT_N1_01_07",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "hide-mask-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 90d,
            Height = 160d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = horizontal,
            VerticalOffsets = vertical,
            Cells = keySet.Cells,
            Topology = new PanelCladdingTopologyState(),
            SourceUserText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase),
            Preview = new PanelPreviewGeometry()
        };
        var repository = new StubRepository(layout);
        var signatures = new PanelCladdingTypeSignatureService(keys);
        var window = new PanelCladdingEditorWindow(new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signatures),
            new PanelPreviewRenderer(),
            signatures));
        Require(window.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Editor fixture did not load.");

        HashSet<string> selected = Field<HashSet<string>>(window, "_selectedExtrusions");
        string[] hiddenIds =
        [
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 80d, 0),
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 80d, 1),
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 120d, 0),
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 120d, 1)
        ];
        selected.UnionWith(hiddenIds);
        Invoke(window, "OnHideExtrusionsClick", window, new System.Windows.RoutedEventArgs());
        Require(Field<HashSet<string>>(window, "_hiddenExtrusions").SetEquals(hiddenIds),
            "Hide action did not record the selected B/C atoms.");
        Require(Field<HashSet<string>>(window, "_deletedExtrusions").Count == 0 &&
                Field<List<double>>(window, "_horizontalOffsets").SequenceEqual(horizontal) &&
                Field<Dictionary<string, string>>(window, "_values").Keys
                    .ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(values.Keys),
            "Hide action deleted segments, collapsed offsets, or renumbered cells.");

        Invoke(window, "OnHideExtrusionsClick", window, new System.Windows.RoutedEventArgs());
        Require(Field<HashSet<string>>(window, "_hiddenExtrusions").Count == 0,
            "Selecting the hidden atoms and pressing Unhide did not restore them.");
        SetField(window, "_dirty", false);
        window.Close();
    }

    private static PanelCladdingTopologyState HiddenFixture() => new()
    {
        HiddenSegments =
        [
            new(PanelCladdingTopologyAxis.Horizontal, 1, 0),
            new(PanelCladdingTopologyAxis.Horizontal, 1, 1),
            new(PanelCladdingTopologyAxis.Horizontal, 2, 0),
            new(PanelCladdingTopologyAxis.Horizontal, 2, 1)
        ]
    };

    private static PanelCladdingMatchPanelSnapshot Snapshot(
        Guid id,
        double width,
        double height,
        IReadOnlyList<double> horizontal,
        IReadOnlyList<double> vertical,
        PanelCladdingTopologyPayloads masks)
    {
        var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.SegmentMaskKey] = masks.SegmentMask,
            [PanelCladdingKeyService.MergeMaskKey] = masks.MergeMask,
            [PanelCladdingKeyService.HideMaskKey] = masks.HideMask
        };
        for (int index = 0; index < horizontal.Count; index++)
        {
            text[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] = horizontal[index].ToString("0.00000");
        }
        for (int index = 0; index < vertical.Count; index++)
        {
            text[PanelCladdingKeyService.GetVerticalOffsetKey(index)] = vertical[index].ToString("0.00000");
        }
        return new PanelCladdingMatchPanelSnapshot
        {
            ObjectId = id,
            Geometry = new PanelCladdingMatchGeometryDescriptor
            {
                GeometryClass = PanelGeometryClass.Planar,
                Width = width,
                Height = height,
                ModelTolerance = 0.001d
            },
            UserText = text
        };
    }

    private static T Required<T>(OperationResponse<T> response)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        return response.Data;
    }

    private static T Field<T>(object instance, string name) where T : class =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T ??
        throw new InvalidOperationException($"Missing field {name}.");

    private static void SetField(object instance, string name, object value) =>
        (instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"Missing field {name}.")).SetValue(instance, value);

    private static void Invoke(object instance, string name, params object[] arguments) =>
        (instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"Missing method {name}.")).Invoke(instance, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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
        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class StubRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used.");
    }
}
