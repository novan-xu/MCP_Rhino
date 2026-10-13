using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingTopologyPersistenceSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        CompactMasksRoundTripAndValidate();
        SaveWritesOnlyNonDefaultMasksAndUsesV4Identity();
        StructuralEditorSaveReloadsDeletedAndMergedCurves();
        OffsetEditsPreserveMasksAndAssignments();

        Console.WriteLine("[OK] One panel-level segment mask and one panel-level merge mask round-trip.");
        Console.WriteLine("[OK] Structural Save writes only nondefault topology masks and a topology-sensitive v4 identity.");
        Console.WriteLine("[OK] Deleted boundaries and merged curve runs survive editor Save/reload.");
        Console.WriteLine("[OK] H/V and row/column edits preserve all masks, cladding, profiles, undo, and scoped save/reload.");
    }

    private static void CompactMasksRoundTripAndValidate()
    {
        var keys = new PanelCladdingKeyService();
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1)
            ],
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 2)
            ]
        };
        PanelCladdingTopologyPayloads payloads = Required(
            keys.EncodeTopology(topology, horizontalTrackCount: 2, verticalTrackCount: 1),
            "Encode topology");
        Require(payloads.SegmentMask.Length > 0 && payloads.MergeMask.Length > 0 && payloads.HideMask.Length > 0,
            "Topology payloads were not produced.");

        PanelCladdingTopologyState decoded = Required(keys.DecodeTopology(
            new Dictionary<string, string>
            {
                [PanelCladdingKeyService.SegmentMaskKey] = payloads.SegmentMask,
                [PanelCladdingKeyService.MergeMaskKey] = payloads.MergeMask,
                [PanelCladdingKeyService.HideMaskKey] = payloads.HideMask
            },
            horizontalTrackCount: 2,
            verticalTrackCount: 1), "Decode topology");
        Require(decoded.MissingSegments.SequenceEqual(topology.MissingSegments),
            "Segment mask did not round-trip the missing segment.");
        Require(decoded.MergeRuns.SequenceEqual(topology.MergeRuns),
            "Merge mask did not round-trip the continuous curve run.");

        PanelCladdingTopologyState legacy = Required(keys.DecodeTopology(
            new Dictionary<string, string>(), 2, 1), "Decode legacy topology");
        Require(legacy.MissingSegments.Count == 0 && legacy.MergeRuns.Count == 0,
            "A panel without masks must default to all segments present and no joins.");
        Require(!keys.DecodeTopology(
                new Dictionary<string, string>
                {
                    [PanelCladdingKeyService.SegmentMaskKey] = payloads.SegmentMask,
                    [PanelCladdingKeyService.MergeMaskKey] = payloads.MergeMask,
                    [PanelCladdingKeyService.HideMaskKey] = payloads.HideMask
                }, 1, 1).Success,
            "A topology payload was accepted for a different lattice.");

        var invalid = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Vertical, 0, 1)
            ],
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 2)
            ]
        };
        Require(!keys.EncodeTopology(invalid, 2, 1).Success,
            "A merged curve was allowed to cross an absent segment.");
    }

    private static void SaveWritesOnlyNonDefaultMasksAndUsesV4Identity()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = BuildLayout(keys, rows: 3, columns: 2);
        var repository = new StatefulRepository(keys, source);
        var signatures = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(repository, signatures);
        double[] horizontal = [8d, 16d, 24d];
        double[] vertical = [10d];
        Dictionary<string, string> values = Values(rows: 4, columns: 2, "MAT-001");
        var topology = new PanelCladdingTopologyState
        {
            MissingSegments =
            [
                new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 1, 1)
            ],
            MergeRuns =
            [
                new PanelCladdingMergeRun(PanelCladdingTopologyAxis.Vertical, 0, 0, 3)
            ]
        };
        Required(save.Save(new PanelCladdingSaveRequest
        {
            FilePath = source.DocumentPath,
            ObjectId = source.ObjectId,
            ExpectedGeometryFingerprint = source.GeometryFingerprint,
            SystemCode = source.SystemCode,
            HorizontalOffsets = horizontal,
            VerticalOffsets = vertical,
            Topology = topology,
            CellValues = values
        }), "Save topology");

        PanelAttributeCommitRequest commit = repository.LastCommit ??
            throw new InvalidOperationException("Save did not commit Rhino attributes.");
        string[] topologyKeys = commit.UserTextWrites.Keys
            .Where(PanelCladdingKeyService.IsTopologyKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        Require(topologyKeys.SequenceEqual(
                new[]
                {
                    PanelCladdingKeyService.FrameConfigKey
                }
                    .OrderBy(key => key, StringComparer.Ordinal)),
            $"Expected combined frame configuration, got {string.Join(",", topologyKeys)}.");
        Require(!commit.UserTextWrites.Keys.Any(key =>
                key.Contains("INT-", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("INT_", StringComparison.OrdinalIgnoreCase)),
            "Save emitted a per-curve topology attribute.");
        Require(commit.UserTextWrites.ContainsKey(PanelCladdingKeyService.GetHorizontalOffsetKey(2)) &&
                commit.UserTextWrites.ContainsKey(PanelCladdingKeyService.GetCellKey(1, "D")),
            "Save did not persist the expanded H grid and new cell keys.");
        PanelCladdingTypeIdentity savedIdentity = Required(
            signatures.Create(repository.Current, values, source.SystemCode),
            "Saved topology identity");
        Require(savedIdentity.StoredSignature.StartsWith("v4:sha256:", StringComparison.Ordinal),
            "Structural Save did not produce the expected v4 type identity.");
        Require(repository.Current.Topology.MissingSegments.SequenceEqual(topology.MissingSegments) &&
                repository.Current.Topology.MergeRuns.SequenceEqual(topology.MergeRuns),
            "The committed mask payloads did not decode into the requested topology.");

        PanelCladdingLayout separateCurves = Clone(repository.Current, new PanelCladdingTopologyState
        {
            MissingSegments = topology.MissingSegments
        });
        PanelCladdingTypeIdentity separateIdentity = Required(
            signatures.Create(separateCurves, values, source.SystemCode), "Separate curve identity");
        PanelCladdingTypeIdentity mergedIdentity = Required(
            signatures.Create(repository.Current, values, source.SystemCode), "Merged curve identity");
        Require(separateIdentity.FullDigest != mergedIdentity.FullDigest,
            "Merge topology did not participate in the type signature.");
    }

    private static void StructuralEditorSaveReloadsDeletedAndMergedCurves()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = BuildLayout(keys, rows: 3, columns: 2);
        var repository = new StatefulRepository(keys, source);
        var signatures = new PanelCladdingTypeSignatureService(keys);
        var controller = new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signatures),
            new PanelPreviewRenderer(),
            signatures);
        var window = new PanelCladdingEditorWindow(controller)
        {
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };
        Required(window.LoadPanel(source.DocumentPath, source.ObjectId), "Load editor");

        HashSet<string> deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        List<PanelExtrusionMergeGroup> merged = Field<List<PanelExtrusionMergeGroup>>(window, "_mergedGroups");
        string deletedId = PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 10d, 1);
        deleted.Add(deletedId);
        string[] verticalIds = Enumerable.Range(0, 3)
            .Select(bay => PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 10d, bay))
            .ToArray();
        merged.Add(new PanelExtrusionMergeGroup(
            $"M:Vertical:10:{string.Join('+', verticalIds)}",
            PanelExtrusionAxis.Vertical,
            10d,
            verticalIds));
        Invoke(window, "MarkStructuralDirty");

        var saveButton = (Button)(window.FindName("SaveButton") ??
            throw new InvalidOperationException("Save button was not found."));
        Require(saveButton.IsEnabled, "Structural edits still disabled Save.");
        Invoke(window, "OnSaveClick", window, new RoutedEventArgs());

        deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        merged = Field<List<PanelExtrusionMergeGroup>>(window, "_mergedGroups");
        Require(deleted.SetEquals([deletedId]), "Deleted extrusion did not survive Save/reload.");
        Require(merged.Count == 1 && merged[0].AtomicIds.SequenceEqual(verticalIds),
            "Merged extrusion run did not survive Save/reload.");
        Require(!FieldValue<bool>(window, "_dirty") && !FieldValue<bool>(window, "_structuralDirty"),
            "Successful structural Save did not return the editor to Saved state.");
        Require(repository.Current.SourceUserText.ContainsKey(PanelCladdingKeyService.FrameConfigKey) &&
                !repository.Current.SourceUserText.Keys.Any(PanelCladdingKeyService.IsRetiredFrameKey),
            "Editor Save did not persist combined configuration and remove legacy masks.");
    }

    private static void OffsetEditsPreserveMasksAndAssignments()
    {
        foreach (string edit in new[] { "H", "V", "Row", "Column" })
        foreach (PanelCladdingSaveScope scope in new[] { PanelCladdingSaveScope.Both, PanelCladdingSaveScope.Extrusions })
        {
            var keys = new PanelCladdingKeyService();
            PanelCladdingLayout source = BuildLayout(keys, rows: 4, columns: 4);
            var repository = new StatefulRepository(keys, source);
            var signatures = new PanelCladdingTypeSignatureService(keys);
            var save = new PanelCladdingSaveService(repository, signatures);
            Dictionary<string, string> values = Values(4, 4, "MAT-001");
            values[PanelCladdingKeyService.GetCellKey(2, "A")] = "1A";
            values[PanelCladdingKeyService.GetCellKey(3, "D")] = "MAT-002";
            var topology = new PanelCladdingTopologyState
            {
                MissingSegments = [new(PanelCladdingTopologyAxis.Horizontal, 0, 0), new(PanelCladdingTopologyAxis.Vertical, 0, 2)],
                HiddenSegments = [new(PanelCladdingTopologyAxis.Horizontal, 2, 3), new(PanelCladdingTopologyAxis.Vertical, 2, 0)],
                MergeRuns = [new(PanelCladdingTopologyAxis.Horizontal, 1, 1, 3), new(PanelCladdingTopologyAxis.Vertical, 1, 0, 2)]
            };
            var assignedSegment = new PanelCladdingSegmentCoordinate(PanelCladdingTopologyAxis.Horizontal, 0, 1);
            var assignments = new PanelFrameAssignmentState
            {
                FrameAssignments = new Dictionary<string, IReadOnlyList<string>> { ["FRM_0"] = new[] { "1D-ALU-001" } },
                SegmentAssignments = [new(assignedSegment, new[] { "1D-ALU-001" })],
                CurveModifiers = new Dictionary<string, double> { [PanelFrameAssignmentService.SegmentKey(assignedSegment)] = 2d }
            };
            Required(save.Save(new PanelCladdingSaveRequest
            {
                FilePath = source.DocumentPath,
                ObjectId = source.ObjectId,
                ExpectedGeometryFingerprint = source.GeometryFingerprint,
                SystemCode = source.SystemCode,
                Topology = topology,
                CellValues = values,
                FrameAssignments = assignments,
                Scope = PanelCladdingSaveScope.Both
            }), "Seed saved masks and assignments");
            var baseline = new Dictionary<string, string>(repository.Current.SourceUserText);
            var expectedMasks = Required(keys.EncodeTopology(topology, 3, 3), "Encode expected masks");
            var controller = new PanelCladdingEditorController(repository, save, new PanelPreviewRenderer(), signatures);
            var window = new PanelCladdingEditorWindow(controller) { ShowInTaskbar = false };
            Required(window.LoadPanel(source.DocumentPath, source.ObjectId), "Load saved offset fixture");
            var baselineValues = new Dictionary<string, string>(Field<Dictionary<string, string>>(window, "_values"));
            AssertRetained("load");

            Invoke(window, "UpdateDividerOffset", PanelDimensionAxis.Row, 0, -1d);
            Invoke(window, "UpdateDividerOffset", PanelDimensionAxis.Row, 0, 10d);
            AssertRetained("invalid/no-op edit");
            Require(Field<System.Collections.ICollection>(window, "_undoStack").Count == 0,
                "Rejected/no-op offsets must not add an undo entry.");

            ApplyEdit();
            AssertRetained("edit");
            PanelCladdingLayout edited = Field<PanelCladdingLayout>(window, "_layout");
            Require((edit is "H" or "Row" ? edited.HorizontalOffsets : edited.VerticalOffsets)[0] == 12d,
                $"{edit} did not move the requested divider.");
            Invoke(window, "UndoLastChange");
            AssertRetained("undo");
            Require(Field<PanelCladdingLayout>(window, "_layout").HorizontalOffsets.SequenceEqual(source.HorizontalOffsets) &&
                    Field<PanelCladdingLayout>(window, "_layout").VerticalOffsets.SequenceEqual(source.VerticalOffsets),
                "Undo did not restore original coordinates.");
            ApplyEdit();
            AssertRetained("repeat edit");
            Invoke(window, "SavePanel", scope);
            AssertStored();
            AssertRetained("save/reload");
            if (scope == PanelCladdingSaveScope.Extrusions)
            {
                Invoke(window, "SavePanel", PanelCladdingSaveScope.Cladding);
                Required(window.LoadPanel(source.DocumentPath, source.ObjectId), "Reload scoped saves");
                AssertStored();
                AssertRetained("scoped reload");
            }
            Require(!FieldValue<bool>(window, "_dirty"), "Successful saves must finish without pending changes.");
            window.Close();

            void ApplyEdit()
            {
                PanelDimensionAxis axis = edit is "H" or "Row" ? PanelDimensionAxis.Row : PanelDimensionAxis.Column;
                if (edit is "H" or "V") Invoke(window, "UpdateDividerOffset", axis, 0, 12d);
                else Invoke(window, "UpdateDimension", axis, 0, 12d, 4d);
            }

            void AssertRetained(string phase)
            {
                PanelCladdingLayout layout = Field<PanelCladdingLayout>(window, "_layout");
                PanelCladdingTopologyPayloads masks = Required(keys.EncodeTopology(layout.Topology, 3, 3), "Encode edited masks");
                Require(masks.SegmentMask == expectedMasks.SegmentMask && masks.MergeMask == expectedMasks.MergeMask &&
                        masks.HideMask == expectedMasks.HideMask, $"{edit}/{scope}/{phase} changed an indexed topology mask.");
                Dictionary<string, string> currentValues = Field<Dictionary<string, string>>(window, "_values");
                Require(currentValues.Count == baselineValues.Count && baselineValues.All(pair =>
                        currentValues.TryGetValue(pair.Key, out string? value) && value == pair.Value),
                    $"{edit}/{scope}/{phase} changed cladding material/parent values.");
                string profilePayload = Required(new PanelFrameAssignmentService().Encode(
                    layout.FrameAssignments, 3, 3, layout.Topology), "Encode edited profiles");
                Require(profilePayload == baseline[PanelCladdingKeyService.FrameTypeKey],
                    $"{edit}/{scope}/{phase} changed extrusion profiles or length modifiers.");
                IReadOnlySet<string> deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
                IReadOnlySet<string> hidden = Field<HashSet<string>>(window, "_hiddenExtrusions");
                Require(deleted.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, layout.HorizontalOffsets[0], 0)) &&
                        deleted.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, layout.VerticalOffsets[0], 2)) &&
                        hidden.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, layout.HorizontalOffsets[2], 3)) &&
                        hidden.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, layout.VerticalOffsets[2], 0)),
                    $"{edit}/{scope}/{phase} left mask display IDs at old coordinates.");
            }

            void AssertStored()
            {
                foreach ((string key, string value) in baseline.Where(pair => PanelCladdingKeyService.IsTopologyKey(pair.Key) ||
                    keys.IsCladdingCellKey(pair.Key) || pair.Key == PanelCladdingKeyService.CladdingLogicKey ||
                    pair.Key == PanelCladdingKeyService.FrameTypeKey))
                {
                    Require(repository.Current.SourceUserText.TryGetValue(key, out string? current) && current == value,
                        $"{edit}/{scope} changed saved layout attribute {key}.");
                }
                IReadOnlyList<double> savedOffsets = edit is "H" or "Row"
                    ? repository.Current.HorizontalOffsets : repository.Current.VerticalOffsets;
                Require(savedOffsets[0] == 12d, "The coordinate change was not saved.");
            }
        }
    }

    private static PanelCladdingLayout BuildLayout(PanelCladdingKeyService keys, int rows, int columns)
    {
        double[] horizontal = Enumerable.Range(1, rows - 1).Select(index => index * 10d).ToArray();
        double[] vertical = Enumerable.Range(1, columns - 1).Select(index => index * 10d).ToArray();
        Dictionary<string, string> values = Values(rows, columns, "MAT-001");
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontal, vertical, values, columns * 10d, rows * 10d, 0.001d), "Build key set");
        var userText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < horizontal.Length; index++)
        {
            userText[PanelCladdingKeyService.GetHorizontalOffsetKey(index)] = PanelCladdingKeyService.FormatOffset(horizontal[index]);
        }
        for (int index = 0; index < vertical.Length; index++)
        {
            userText[PanelCladdingKeyService.GetVerticalOffsetKey(index)] = PanelCladdingKeyService.FormatOffset(vertical[index]);
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-topology-persistence.3dm",
            ObjectName = "PID-TOPOLOGY-PERSIST",
            LayerFullPath = "CLDG::WT01",
            SystemCode = "WT01",
            GeometryFingerprint = "topology-persistence-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = columns * 10d,
            Height = rows * 10d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology,
            SourceUserText = userText,
            Preview = new PanelPreviewGeometry(),
            WorkbookPath = string.Empty
        };
    }

    private static Dictionary<string, string> Values(int rows, int columns, string material)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                result[PanelCladdingKeyService.GetCellKey(column, PanelCladdingKeyService.GetRowLabel(row))] = material;
            }
        }
        return result;
    }

    private static PanelCladdingLayout Clone(PanelCladdingLayout source, PanelCladdingTopologyState topology) => new()
    {
        ObjectId = source.ObjectId,
        DocumentRuntimeSerialNumber = source.DocumentRuntimeSerialNumber,
        DocumentPath = source.DocumentPath,
        ObjectName = source.ObjectName,
        LayerFullPath = source.LayerFullPath,
        SystemCode = source.SystemCode,
        GeometryFingerprint = source.GeometryFingerprint,
        GeometryClass = source.GeometryClass,
        GeometryDiagnostic = source.GeometryDiagnostic,
        Width = source.Width,
        Height = source.Height,
        ModelTolerance = source.ModelTolerance,
        ModelUnitScaleToMillimeters = source.ModelUnitScaleToMillimeters,
        HorizontalOffsets = source.HorizontalOffsets,
        VerticalOffsets = source.VerticalOffsets,
        Cells = source.Cells,
        Topology = topology,
        SourceUserText = source.SourceUserText,
        Preview = source.Preview,
        WorkbookPath = source.WorkbookPath
    };

    private static T Required<T>(OperationResponse<T> response, string operation) where T : class =>
        response.Success && response.Data is not null
            ? response.Data
            : throw new InvalidOperationException($"{operation} failed: {response.Message}");

    private static void Required(OperationResponse response, string operation)
    {
        if (!response.Success)
        {
            throw new InvalidOperationException($"{operation} failed: {response.Message}");
        }
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static T FieldValue<T>(object instance, string name) where T : struct =>
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

    private sealed class StatefulRepository : ILivePanelCladdingRepository
    {
        private readonly PanelCladdingKeyService _keys;

        public StatefulRepository(PanelCladdingKeyService keys, PanelCladdingLayout current)
        {
            _keys = keys;
            Current = current;
        }

        public PanelCladdingLayout Current { get; private set; }
        public PanelAttributeCommitRequest? LastCommit { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(Current);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by topology persistence smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            LastCommit = request;
            OperationResponse finalized = finalizeExternalCommit();
            if (!finalized.Success)
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail(finalized.Message);
            }
            var userText = new Dictionary<string, string>(Current.SourceUserText, StringComparer.OrdinalIgnoreCase);
            foreach (string key in request.UserTextDeletes)
            {
                userText.Remove(key);
            }
            foreach ((string key, string value) in request.UserTextWrites)
            {
                userText[key] = value;
            }
            PanelCladdingKeySet parsed = Required(_keys.Parse(
                userText, Current.Width, Current.Height, Current.ModelTolerance), "Parse committed user text");
            Current = new PanelCladdingLayout
            {
                ObjectId = Current.ObjectId,
                DocumentRuntimeSerialNumber = Current.DocumentRuntimeSerialNumber,
                DocumentPath = Current.DocumentPath,
                ObjectName = Current.ObjectName,
                LayerFullPath = Current.LayerFullPath,
                SystemCode = Current.SystemCode,
                GeometryFingerprint = Current.GeometryFingerprint,
                GeometryClass = Current.GeometryClass,
                GeometryDiagnostic = Current.GeometryDiagnostic,
                Width = Current.Width,
                Height = Current.Height,
                ModelTolerance = Current.ModelTolerance,
                ModelUnitScaleToMillimeters = Current.ModelUnitScaleToMillimeters,
                HorizontalOffsets = parsed.HorizontalOffsets,
                VerticalOffsets = parsed.VerticalOffsets,
                Cells = parsed.Cells,
                Topology = parsed.Topology,
                FrameAssignments = parsed.FrameAssignments,
                SourceUserText = userText,
                Preview = Current.Preview,
                WorkbookPath = Current.WorkbookPath
            };
            return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
            {
                ObjectId = request.ObjectId,
                Mutated = true
            });
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by topology persistence smoke.");
    }
}
