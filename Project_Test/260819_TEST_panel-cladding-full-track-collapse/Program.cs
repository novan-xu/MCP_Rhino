using System.Reflection;
using System.Windows;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingFullTrackCollapseSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NormalizerRemovesCompleteTrackAndRenumbersCells();
        NormalizerAlsoCollapsesVerticalTrack();
        SaveDeletesObsoleteGridAndRefreshesDimensions();
        SpawnPlanExpandsPartialLogicalCell();
        EditorImmediatelyCollapsesCompleteTrack();
        EditorNormalizesLegacyCompleteMaskOnLoad();

        Console.WriteLine("[OK] complete H tracks collapse while partial tracks remain masked.");
        Console.WriteLine("[OK] cells, parents, offsets, masks, and unit dimensions are saved from the normalized layout.");
        Console.WriteLine("[OK] PCSpawnSrf expands logical cells across hidden physical members.");
        Console.WriteLine("[OK] PCEditor updates rows, labels, dimensions, and Undo immediately.");
        Console.WriteLine("[OK] previously saved complete-track masks normalize when the editor loads.");
    }

    private static void NormalizerRemovesCompleteTrackAndRenumbersCells()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = Values();
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1),
                new(PanelCladdingTopologyAxis.Horizontal, 2, 0)
            ]
        };
        PanelCladdingTopologyNormalizationResult result = Required(
            new PanelCladdingTopologyNormalizationService(keys).Normalize(
                [24.84375d, 97.875d, 182.75d],
                [45d],
                values,
                topology,
                90d,
                195.75d,
                0.001d),
            "Normalize complete H0");

        Require(result.HorizontalOffsets.SequenceEqual([97.875d, 182.75d]),
            $"Unexpected normalized H offsets: {string.Join(",", result.HorizontalOffsets)}");
        Require(result.VerticalOffsets.SequenceEqual([45d]), "V0 changed during H collapse.");
        Require(result.CollapsedTracks.SequenceEqual(
                [new PanelCladdingCollapsedTrack(PanelCladdingTopologyAxis.Horizontal, 0)]),
            "The complete H0 track was not reported exactly once.");
        Require(result.CellValues[Key(0, "A")].Length == 0,
            "The affected merged A/B assignment should remain cleared by editor semantics.");
        Require(result.CellValues[Key(0, "B")] == "MPL-001" &&
                result.CellValues[Key(0, "C")] == "TER-001",
            "Old C/D assignments did not shift to new B/C.");
        Require(result.CellValues[Key(1, "B")] == "0B" &&
                result.CellValues[Key(1, "C")] == "0C",
            "Parent assignments did not shift with their owner labels.");
        Require(result.Topology.MissingSegments.SequenceEqual(
                [new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 1, 0)]),
            "Remaining partial H2 segment was not remapped to normalized H1.");
    }

    private static void SaveDeletesObsoleteGridAndRefreshesDimensions()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = Values(clearMergedRows: false);
        PanelCladdingLayout source = Layout(keys, values);
        var repository = new CapturingRepository(source);
        var save = new PanelCladdingSaveService(repository, new PanelCladdingTypeSignatureService(keys));
        Required(save.Save(new PanelCladdingSaveRequest
        {
            FilePath = source.DocumentPath,
            ObjectId = source.ObjectId,
            ExpectedGeometryFingerprint = source.GeometryFingerprint,
            SystemCode = source.SystemCode,
            HorizontalOffsets = source.HorizontalOffsets,
            VerticalOffsets = source.VerticalOffsets,
            Topology = new PanelCladdingTopologyState
            {
                MissingSegments =
                [
                    new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                    new(PanelCladdingTopologyAxis.Horizontal, 0, 1)
                ]
            },
            CellValues = values
        }), "Save normalized track");

        PanelAttributeCommitRequest commit = repository.LastCommit ??
            throw new InvalidOperationException("Save did not prepare a commit.");
        Require(commit.UserTextWrites[PanelCladdingKeyService.GetHorizontalOffsetKey(0)] == "97.875" &&
                commit.UserTextWrites[PanelCladdingKeyService.GetHorizontalOffsetKey(1)] == "182.75" &&
                !commit.UserTextWrites.ContainsKey(PanelCladdingKeyService.GetHorizontalOffsetKey(2)),
            "Save did not persist the reduced H lattice.");
        Require(commit.UserTextDeletes.Contains(PanelCladdingKeyService.GetHorizontalOffsetKey(2), StringComparer.OrdinalIgnoreCase),
            "Obsolete H2 was not deleted.");
        Require(commit.UserTextDeletes.Contains(Key(0, "D"), StringComparer.OrdinalIgnoreCase) &&
                commit.UserTextDeletes.Contains(Key(1, "D"), StringComparer.OrdinalIgnoreCase),
            "Obsolete D cell keys were not deleted.");
        Require(commit.UserTextWrites[Key(0, "B")] == "MPL-001" &&
                commit.UserTextWrites[Key(0, "C")] == "TER-001",
            "Save did not write renumbered C/D assignments as B/C.");
        Require(commit.UserTextWrites[PanelCladdingKeyService.UnitWidthKey] == "90.00000" &&
                commit.UserTextWrites[PanelCladdingKeyService.UnitHeightKey] == "195.75000" &&
                commit.UserTextWrites[PanelCladdingKeyService.UnitDimensionKey] == "90.00000x195.75000",
            "Save did not refresh unit dimension attributes.");
    }

    private static void NormalizerAlsoCollapsesVerticalTrack()
    {
        var keys = new PanelCladdingKeyService();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Key(0, "A")] = "MPL-000",
            [Key(0, "B")] = "MPL-000",
            [Key(1, "A")] = "MPL-001",
            [Key(1, "B")] = "MPL-001",
            [Key(2, "A")] = "TER-001",
            [Key(2, "B")] = "2A"
        };
        PanelCladdingTopologyNormalizationResult result = Required(
            new PanelCladdingTopologyNormalizationService(keys).Normalize(
                [10d],
                [10d, 20d],
                values,
                new PanelCladdingTopologyState
                {
                    MissingSegments =
                    [
                        new(PanelCladdingTopologyAxis.Vertical, 0, 0),
                        new(PanelCladdingTopologyAxis.Vertical, 0, 1),
                        new(PanelCladdingTopologyAxis.Horizontal, 0, 2)
                    ]
                },
                30d,
                20d,
                0.001d),
            "Normalize complete V0");
        Require(result.VerticalOffsets.SequenceEqual([20d]), "Complete V0 was not removed.");
        Require(result.CellValues[Key(1, "A")] == "TER-001" &&
                result.CellValues[Key(1, "B")] == "1A",
            "Cells and parent labels right of the removed V0 were not renumbered.");
        Require(result.Topology.MissingSegments.SequenceEqual(
                [new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1)]),
            "Opposite-axis bay coordinates were not remapped after V collapse.");
    }

    private static void SpawnPlanExpandsPartialLogicalCell()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingKeySet physical = Required(keys.CreateKeySet(
            [10d],
            Array.Empty<double>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Key(0, "A")] = "MPL-001"
            },
            10d,
            20d,
            0.001d), "Build partial logical fixture");
        var keySet = new PanelCladdingKeySet
        {
            HorizontalOffsets = physical.HorizontalOffsets,
            VerticalOffsets = physical.VerticalOffsets,
            Cells = physical.Cells,
            Topology = new PanelCladdingTopologyState
            {
                MissingSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0)]
            }
        };
        PanelCladdingSpawnPlan plan = Required(new PanelCladdingSpawnPlanningService(keys).CreatePlan(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_TEST_01",
                [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "REL-01",
                [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT-01"
            },
            keySet), "Plan logical surface");
        Require(plan.Regions.Count == 1 &&
                plan.Regions[0].OwnerCellLabel == "0A" &&
                plan.Regions[0].Cells.Select(cell => cell.ShortLabel).SequenceEqual(["0A", "0B"]),
            "Spawn plan did not cover both physical members of logical 0A.");
    }

    private static void EditorImmediatelyCollapsesCompleteTrack()
    {
        var keys = new PanelCladdingKeyService();
        Dictionary<string, string> values = Values(clearMergedRows: false);
        PanelCladdingLayout source = Layout(keys, values);
        var repository = new CapturingRepository(source);
        var signatures = new PanelCladdingTypeSignatureService(keys);
        var renderer = new PanelPreviewRenderer();
        var window = new PanelCladdingEditorWindow(new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signatures),
            renderer,
            signatures))
        {
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        Required(window.LoadPanel(source.DocumentPath, source.ObjectId), "Load editor");
        HashSet<string> selected = Field<HashSet<string>>(window, "_selectedExtrusions");
        selected.Add(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 24.84375d, 0));
        selected.Add(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 24.84375d, 1));
        Invoke(window, "OnDeleteExtrusionsClick", window, new RoutedEventArgs());

        PanelCladdingLayout working = Field<PanelCladdingLayout>(window, "_layout");
        Dictionary<string, string> editorValues = Field<Dictionary<string, string>>(window, "_values");
        List<bool> rowLocks = Field<List<bool>>(window, "_rowLocks");
        Require(working.RowCount == 3 && working.HorizontalOffsets.SequenceEqual([97.875d, 182.75d]),
            "Editor retained the fully deleted H0 point.");
        Require(rowLocks.Count == 3, "Editor retained the fourth row dimension/lock.");
        Require(editorValues[Key(0, "B")] == "MPL-001" && editorValues[Key(0, "C")] == "TER-001",
            "Editor did not immediately renumber old C/D as B/C.");

        Invoke(window, "UndoLastChange");
        working = Field<PanelCladdingLayout>(window, "_layout");
        Require(working.RowCount == 4 && working.HorizontalOffsets.SequenceEqual(source.HorizontalOffsets),
            "Undo did not restore the collapsed track.");
    }

    private static void EditorNormalizesLegacyCompleteMaskOnLoad()
    {
        var keys = new PanelCladdingKeyService();
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new(PanelCladdingTopologyAxis.Horizontal, 0, 0),
                new(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ]
        };
        PanelCladdingLayout source = Layout(keys, Values(clearMergedRows: false), topology);
        var repository = new CapturingRepository(source);
        var signatures = new PanelCladdingTypeSignatureService(keys);
        var renderer = new PanelPreviewRenderer();
        var window = new PanelCladdingEditorWindow(new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signatures),
            renderer,
            signatures))
        {
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        Required(window.LoadPanel(source.DocumentPath, source.ObjectId), "Load legacy complete mask");
        PanelCladdingLayout working = Field<PanelCladdingLayout>(window, "_layout");
        Require(working.RowCount == 3 && working.HorizontalOffsets.SequenceEqual([97.875d, 182.75d]),
            "Legacy complete-track mask did not normalize during load.");
    }

    private static Dictionary<string, string> Values(bool clearMergedRows = true) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Key(0, "A")] = clearMergedRows ? string.Empty : "MPL-001",
            [Key(0, "B")] = clearMergedRows ? string.Empty : "MPL-001",
            [Key(0, "C")] = "MPL-001",
            [Key(0, "D")] = "TER-001",
            [Key(1, "A")] = clearMergedRows ? string.Empty : "0A",
            [Key(1, "B")] = clearMergedRows ? string.Empty : "0B",
            [Key(1, "C")] = "0C",
            [Key(1, "D")] = "0D"
        };

    private static PanelCladdingLayout Layout(
        PanelCladdingKeyService keys,
        IReadOnlyDictionary<string, string> values,
        PanelCladdingTopologyState? topology = null)
    {
        double[] horizontal = [24.84375d, 97.875d, 182.75d];
        double[] vertical = [45d];
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontal,
            vertical,
            values,
            90d,
            195.75d,
            0.001d), "Build 2x4 layout");
        var sourceText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.UnitWidthKey] = " ",
            [PanelCladdingKeyService.UnitHeightKey] = " ",
            [PanelCladdingKeyService.UnitDimensionKey] = " "
        };
        for (int index = 0; index < horizontal.Length; index++)
        {
            sourceText[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] =
                PanelCladdingKeyService.FormatOffset(horizontal[index]);
        }
        sourceText[PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "45";
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/full-track-collapse.3dm",
            ObjectName = "N1_PF_07",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "full-track-collapse-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 90d,
            Height = 195.75d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = topology ?? new PanelCladdingTopologyState(),
            SourceUserText = sourceText,
            Preview = new PanelPreviewGeometry()
        };
    }

    private static string Key(int column, string row) => PanelCladdingKeyService.GetCellKey(column, row);

    private static T Required<T>(OperationResponse<T> response, string operation)
    {
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response.Data;
    }

    private static OperationResponse Required(OperationResponse response, string operation)
    {
        if (!response.Success)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
        return response;
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        method.Invoke(instance, arguments);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeExternalCommit();
            return finalized.Success
                ? OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
                {
                    ObjectId = request.ObjectId,
                    Mutated = true
                })
                : OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used.");
    }
}
