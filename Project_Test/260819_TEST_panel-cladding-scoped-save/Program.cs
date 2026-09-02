using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingScopedSaveSmoke;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        MissingMasksRepresentTheDefaultTopology();
        SaveScopesMutateOnlyTheirOwnedAttributes();
        EditorFooterExposesThreeScopedSaveActions();

        Console.WriteLine("[OK] absent topology masks permit default extrusion planning.");
        Console.WriteLine("[OK] scoped saves preserve sparse default topology without 2.05-2.07 attributes.");
        Console.WriteLine("[OK] editor footer exposes Save Extrusions, Save Cladding, and Save Both with no Exit action.");
        return 0;
    }

    private static void MissingMasksRepresentTheDefaultTopology()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = BuildLayout(keys);
        var panelText = new Dictionary<string, string>(source.SourceUserText, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingSpawnPlanningService.PanelIdUserTextKey] = "PID_TEST_01",
            [PanelCladdingSpawnPlanningService.CidUserTextKey] = "CID_TEST_01",
            [PanelCladdingSpawnPlanningService.ReleaseUserTextKey] = "01",
            [PanelCladdingSpawnPlanningService.WallTypeUserTextKey] = "WT01"
        };
        var planner = new PanelCladdingSpawnPlanningService(keys);
        OperationResponse<PanelCladdingSpawnPlan> planned = planner.CreatePlan(
            panelText,
            new PanelCladdingKeySet
            {
                HorizontalOffsets = source.HorizontalOffsets,
                VerticalOffsets = source.VerticalOffsets,
                Cells = source.Cells,
                Topology = source.Topology
            },
            source.Width,
            source.Height);
        Require(planned.Success && planned.Data is not null && planned.Data.Curves.Count > 0,
            $"Absent masks did not plan default extrusion curves: {planned.Message}");
    }

    private static void SaveScopesMutateOnlyTheirOwnedAttributes()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = BuildLayout(keys);

        PanelAttributeCommitRequest cladding = Save(source, PanelCladdingSaveScope.Cladding);
        Require(!cladding.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey) &&
                !cladding.UserTextDeletes.Any(PanelCladdingKeyService.IsTopologyKey),
            "Cladding-only save created or deleted topology masks.");
        Require(!cladding.UserTextWrites.Keys.Any(keys.IsOffsetKey) &&
                !cladding.UserTextDeletes.Any(keys.IsOffsetKey),
            "Cladding-only save changed extrusion offsets.");
        Require(source.Cells.All(cell => cladding.UserTextWrites.ContainsKey(cell.UserTextKey)) &&
                cladding.UserTextWrites.ContainsKey(PanelCladdingKeyService.TypeCodeKey) &&
                !cladding.UserTextWrites.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                cladding.UserTextDeletes.Contains(
                    PanelCladdingKeyService.SignatureKey,
                    StringComparer.OrdinalIgnoreCase),
            "Cladding-only save omitted logical cells/type or did not retire Signature.");

        PanelAttributeCommitRequest extrusions = Save(source, PanelCladdingSaveScope.Extrusions);
        Require(!extrusions.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "Extrusion-only save persisted redundant default topology masks.");
        Require(extrusions.UserTextWrites.Keys.Any(keys.IsOffsetKey),
            "Extrusion-only save omitted offsets.");
        Require(!extrusions.UserTextWrites.Keys.Any(keys.IsCladdingCellKey) &&
                !extrusions.UserTextDeletes.Any(keys.IsCladdingCellKey) &&
                !extrusions.UserTextWrites.ContainsKey(PanelCladdingKeyService.TypeCodeKey) &&
                !extrusions.UserTextWrites.ContainsKey(PanelCladdingKeyService.SignatureKey) &&
                extrusions.UserTextDeletes.Contains(
                    PanelCladdingKeyService.SignatureKey,
                    StringComparer.OrdinalIgnoreCase),
            "Extrusion-only save changed cladding assignments/type or retained Signature.");

        PanelAttributeCommitRequest both = Save(source, PanelCladdingSaveScope.Both);
        Require(!both.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey) &&
                both.UserTextWrites.Keys.Any(keys.IsCladdingCellKey) &&
                both.UserTextWrites.Keys.Any(keys.IsOffsetKey),
            "Save Both did not include both scopes with sparse default topology.");
    }

    private static void EditorFooterExposesThreeScopedSaveActions()
    {
        var keys = new PanelCladdingKeyService();
        PanelCladdingLayout source = BuildLayout(keys);
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

        Button extrusion = Named<Button>(window, "SaveExtrusionsButton");
        Button cladding = Named<Button>(window, "SaveCladdingButton");
        Button both = Named<Button>(window, "SaveButton");
        Require((string)extrusion.Content == "Save Extrusions" &&
                (string)cladding.Content == "Save Cladding" &&
                (string)both.Content == "Save Both",
            "Editor save button labels do not match the requested scopes.");
        Require(FindVisualChildren<Button>(window).All(button =>
                !string.Equals(button.Content?.ToString(), "Exit", StringComparison.OrdinalIgnoreCase)),
            "The editor still exposes an Exit button.");

        Invoke(window, "OnSaveExtrusionsClick", window, new RoutedEventArgs());
        Require(!repository.Current.SourceUserText.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "Save Extrusions created redundant default topology attributes.");
        Require(!FieldValue<bool>(window, "_structuralDirty"),
            "Save Extrusions did not clear extrusion dirty state.");
        Invoke(window, "OnSaveCladdingClick", window, new RoutedEventArgs());
        Require(repository.CommitCount == 2 && repository.LastCommit is not null &&
                !repository.LastCommit.UserTextWrites.Keys.Any(PanelCladdingKeyService.IsTopologyKey) &&
                !repository.Current.SourceUserText.Keys.Any(PanelCladdingKeyService.IsTopologyKey),
            "A partial extrusion save did not refresh the source fingerprint for a following cladding save.");
        window.Close();
    }

    private static PanelAttributeCommitRequest Save(
        PanelCladdingLayout source,
        PanelCladdingSaveScope scope)
    {
        var keys = new PanelCladdingKeyService();
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
            Topology = source.Topology,
            CellValues = source.Cells.ToDictionary(
                cell => cell.UserTextKey,
                cell => cell.Value,
                StringComparer.OrdinalIgnoreCase),
            Scope = scope
        }), $"Save {scope}");
        return repository.LastCommit ?? throw new InvalidOperationException("Save did not prepare a commit.");
    }

    private static PanelCladdingLayout BuildLayout(PanelCladdingKeyService keys)
    {
        double[] horizontal = [20d];
        double[] vertical = [15d];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 2; row++)
            {
                values[PanelCladdingKeyService.GetCellKey(
                    column,
                    PanelCladdingKeyService.GetRowLabel(row))] = "MPL-001";
            }
        }
        PanelCladdingKeySet keySet = Required(keys.CreateKeySet(
            horizontal, vertical, values, 30d, 40d, 0.001d), "Build key set");
        var userText = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.GetHorizontalOffsetKey(0)] = "20.00000",
            [PanelCladdingKeyService.GetVerticalOffsetKey(0)] = "15.00000",
            [PanelCladdingKeyService.TypeCodeKey] = "WT01-2X2-EXISTING",
            [PanelCladdingKeyService.SignatureKey] = "v4:sha256:existing"
        };
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-scoped-save.3dm",
            ObjectName = "PID_SCOPED_SAVE",
            LayerFullPath = "01_CW Panels::Surfaces-PNL::WT-01",
            SystemCode = "WT01",
            GeometryFingerprint = "scoped-save-fingerprint",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = 30d,
            Height = 40d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Topology = keySet.Topology,
            SourceUserText = userText,
            Preview = new PanelPreviewGeometry()
        };
    }

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

    private static T Named<T>(FrameworkElement root, string name) where T : FrameworkElement =>
        root.FindName(name) as T ?? throw new InvalidOperationException($"Missing {name}.");

    private static T FieldValue<T>(object instance, string name) where T : struct =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        method.Invoke(instance, arguments);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private class CapturingRepository(PanelCladdingLayout current) : ILivePanelCladdingRepository
    {
        public PanelCladdingLayout Current { get; protected set; } = current;
        public PanelAttributeCommitRequest? LastCommit { get; protected set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(Current);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");

        public virtual OperationResponse<PanelAttributeCommitResult> CommitAttributes(
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

    private sealed class StatefulRepository(
        PanelCladdingKeyService keys,
        PanelCladdingLayout current) : CapturingRepository(current)
    {
        public int CommitCount { get; private set; }

        public override OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            if (!string.Equals(
                request.ExpectedGeometryFingerprint,
                Current.GeometryFingerprint,
                StringComparison.Ordinal))
            {
                return OperationResponse<PanelAttributeCommitResult>.Fail("Stale test editor fingerprint.");
            }
            OperationResponse<PanelAttributeCommitResult> committed =
                base.CommitAttributes(request, finalizeExternalCommit);
            if (!committed.Success)
            {
                return committed;
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
            PanelCladdingKeySet parsed = Required(keys.Parse(
                userText, Current.Width, Current.Height, Current.ModelTolerance), "Parse committed panel");
            CommitCount++;
            Current = new PanelCladdingLayout
            {
                ObjectId = Current.ObjectId,
                DocumentRuntimeSerialNumber = Current.DocumentRuntimeSerialNumber,
                DocumentPath = Current.DocumentPath,
                ObjectName = Current.ObjectName,
                LayerFullPath = Current.LayerFullPath,
                SystemCode = Current.SystemCode,
                GeometryFingerprint = $"scoped-save-fingerprint-{CommitCount}",
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
                SourceUserText = userText,
                Preview = Current.Preview,
                WorkbookPath = Current.WorkbookPath
            };
            return committed;
        }
    }
}
