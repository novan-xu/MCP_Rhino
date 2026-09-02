using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingCellTopologySmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        DeletedBoundaryMergesAndUndoRestoresCells();
        ConnectedAndNonRectangularTopologyUsesCanonicalLabels();
        AddHorizontalCreatesAndRenumbersOnlyTargetCells();
        AddVerticalCreatesAndRenumbersOnlyTargetCells();

        Console.WriteLine("[OK] Deleted horizontal and vertical INT boundaries produce canonical logical cells.");
        Console.WriteLine("[OK] Deletion clears the complete affected group, renders one spanning cell, and is undoable.");
        Console.WriteLine("[OK] Add H creates/renumbers rows without phantom neighboring cells.");
        Console.WriteLine("[OK] Add V creates/renumbers columns without phantom neighboring cells.");
    }

    private static void DeletedBoundaryMergesAndUndoRestoresCells()
    {
        PanelCladdingLayout source = BuildLayout(columns: 2, rows: 4);
        PanelCladdingEditorWindow window = BuildWindow(source);
        Invoke(window, "SwitchView", PanelEditorView.Extrusion, false);

        HashSet<string> selectedCells = Field<HashSet<string>>(window, "_selectedKeys");
        selectedCells.Clear();
        selectedCells.Add(Key(0, "D"));
        HashSet<string> selectedExtrusions = Field<HashSet<string>>(window, "_selectedExtrusions");
        selectedExtrusions.Clear();
        selectedExtrusions.Add(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 30d, 0));
        Invoke(window, "OnDeleteExtrusionsClick", window, new RoutedEventArgs());

        PanelCladdingLayout working = Field<PanelCladdingLayout>(window, "_layout");
        HashSet<string> deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        PanelCladdingCellTopology topology = PanelCladdingCellTopology.Build(working, deleted);
        PanelCladdingCellGroup merged = Required(topology.FindByLabel("0D"), "Deleted 0C/0D boundary did not form a group.");
        Require(merged.Representative.ShortLabel == "0C", "0C/0D must use 0C as the canonical label.");
        Require(merged.Cells.Select(cell => cell.ShortLabel).OrderBy(label => label).SequenceEqual(["0C", "0D"]),
            "Deleted boundary did not merge exactly 0C and 0D.");
        Require(topology.Groups.Count == 7,
            "A partial boundary deletion in a 2x4 grid must expose seven logical cells.");

        Dictionary<string, string> values = Field<Dictionary<string, string>>(window, "_values");
        Require(values[Key(0, "C")].Length == 0 && values[Key(0, "D")].Length == 0,
            "Both primitive members of the merged cell must be unassigned.");
        Require(selectedCells.SetEquals([Key(0, "C")]), "Selection was not normalized from 0D to canonical cell 0C.");

        FrameworkElement root = (FrameworkElement)window.Content;
        Render(root, 1200, 900, OutputPath("panel-cell-topology-delete-1200x900.png"));
        PanelCladdingGridCanvas canvas = Descendants<PanelCladdingGridCanvas>(root).Single();
        Dictionary<string, Rect> cellRects = Field<Dictionary<string, Rect>>(canvas, "_cellRects");
        Require(cellRects[Key(0, "C")] == cellRects[Key(0, "D")],
            "The merged 0C/0D members were not rendered as one spanning rectangle.");

        Invoke(window, "UndoLastChange");
        working = Field<PanelCladdingLayout>(window, "_layout");
        deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        topology = PanelCladdingCellTopology.Build(working, deleted);
        values = Field<Dictionary<string, string>>(window, "_values");
        Require(deleted.Count == 0 && topology.Groups.Count == 8,
            "Undo did not restore the original 2x4 topology.");
        Require(values[Key(0, "C")] == "MAT-0C" && values[Key(0, "D")] == "MAT-0D",
            "Undo did not restore the assignments cleared by deletion.");
    }

    private static void ConnectedAndNonRectangularTopologyUsesCanonicalLabels()
    {
        PanelCladdingLayout vertical = BuildLayout(columns: 3, rows: 1);
        HashSet<string> deletedVertical =
        [
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 10d, 0),
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 20d, 0)
        ];
        PanelCladdingCellTopology verticalTopology = PanelCladdingCellTopology.Build(vertical, deletedVertical);
        Require(verticalTopology.Groups.Count == 1, "A deleted vertical chain must form one logical cell.");
        Require(verticalTopology.Groups[0].Representative.ShortLabel == "0A",
            "A vertical chain must keep the leftmost canonical label.");

        PanelCladdingLayout square = BuildLayout(columns: 2, rows: 2);
        HashSet<string> deletedL =
        [
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 10d, 0),
            PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 10d, 1)
        ];
        PanelCladdingCellTopology lTopology = PanelCladdingCellTopology.Build(square, deletedL);
        PanelCladdingCellGroup lGroup = Required(lTopology.FindByLabel("1B"), "The L-shaped deleted boundary group is missing.");
        Require(lGroup.Representative.ShortLabel == "0A" && lGroup.Cells.Count == 3 && !lGroup.IsRectangular,
            "Non-rectangular groups must remain connected under one deterministic canonical label.");
    }

    private static void AddHorizontalCreatesAndRenumbersOnlyTargetCells()
    {
        PanelCladdingLayout source = BuildLayout(
            columns: 2,
            rows: 3,
            overrides: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Key(1, "C")] = "0C"
            });
        PanelCladdingEditorWindow window = BuildWindow(source);
        Invoke(window, "AddMullion", PanelExtrusionAxis.Horizontal, 15d, Key(0, "B"));

        PanelCladdingLayout working = Field<PanelCladdingLayout>(window, "_layout");
        HashSet<string> deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        Dictionary<string, string> values = Field<Dictionary<string, string>>(window, "_values");
        PanelCladdingCellTopology topology = PanelCladdingCellTopology.Build(working, deleted);
        string[] logicalLabels = topology.Groups.Select(group => group.Representative.ShortLabel).OrderBy(label => label).ToArray();

        Require(working.RowCount == 4 && working.ColumnCount == 2, "Add H did not create a new regular-grid row.");
        Require(deleted.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Horizontal, 15d, 1)),
            "Add H did not hide the new segment outside the target column.");
        Require(logicalLabels.SequenceEqual(["0A", "0B", "0C", "0D", "1A", "1B", "1D"]),
            $"Add H logical labels are wrong: {string.Join(",", logicalLabels)}");
        PanelCladdingCellGroup unaffected = Required(topology.FindByLabel("1C"), "Unsegmented neighboring H bay is missing.");
        Require(unaffected.Representative.ShortLabel == "1B" && unaffected.Cells.Count == 2,
            "The neighboring H bay exposed a phantom split instead of remaining logical cell 1B.");
        Require(values[Key(0, "B")].Length == 0 && values[Key(0, "C")].Length == 0,
            "The two new H target cells must be unassigned.");
        Require(values[Key(0, "D")] == "MAT-0C", "The cell above Add H was not renumbered from 0C to 0D.");
        Require(values[Key(1, "D")] == "0D", "A parent reference above Add H was not renumbered to 0D.");
        Render((FrameworkElement)window.Content, 1200, 900, OutputPath("panel-cell-topology-add-h-1200x900.png"));

        Invoke(window, "UndoLastChange");
        working = Field<PanelCladdingLayout>(window, "_layout");
        deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        Require(working.RowCount == 3 && deleted.Count == 0, "Undo did not restore the pre-Add-H topology.");
    }

    private static void AddVerticalCreatesAndRenumbersOnlyTargetCells()
    {
        PanelCladdingLayout source = BuildLayout(
            columns: 2,
            rows: 3,
            overrides: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Key(1, "C")] = "1B"
            });
        PanelCladdingEditorWindow window = BuildWindow(source);
        Invoke(window, "AddMullion", PanelExtrusionAxis.Vertical, 5d, Key(0, "B"));

        PanelCladdingLayout working = Field<PanelCladdingLayout>(window, "_layout");
        HashSet<string> deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        Dictionary<string, string> values = Field<Dictionary<string, string>>(window, "_values");
        PanelCladdingCellTopology topology = PanelCladdingCellTopology.Build(working, deleted);
        string[] logicalLabels = topology.Groups.Select(group => group.Representative.ShortLabel).OrderBy(label => label).ToArray();

        Require(working.ColumnCount == 3 && working.RowCount == 3, "Add V did not create a new regular-grid column.");
        Require(deleted.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 5d, 0)) &&
                deleted.Contains(PanelExtrusionTopology.AtomicId(PanelExtrusionAxis.Vertical, 5d, 2)),
            "Add V did not hide the new segments outside the target row.");
        Require(logicalLabels.SequenceEqual(["0A", "0B", "0C", "1B", "2A", "2B", "2C"]),
            $"Add V logical labels are wrong: {string.Join(",", logicalLabels)}");
        Require(Required(topology.FindByLabel("1A"), "Lower neighboring V bay is missing.").Representative.ShortLabel == "0A" &&
                Required(topology.FindByLabel("1C"), "Upper neighboring V bay is missing.").Representative.ShortLabel == "0C",
            "Neighboring V bays exposed phantom splits instead of retaining their left labels.");
        Require(values[Key(0, "B")].Length == 0 && values[Key(1, "B")].Length == 0,
            "The two new V target cells must be unassigned.");
        Require(values[Key(2, "A")] == "MAT-1A", "The cell right of Add V was not renumbered from 1A to 2A.");
        Require(values[Key(2, "C")] == "2B", "A parent reference right of Add V was not renumbered to column 2.");
        Render((FrameworkElement)window.Content, 1200, 900, OutputPath("panel-cell-topology-add-v-1200x900.png"));

        Invoke(window, "UndoLastChange");
        working = Field<PanelCladdingLayout>(window, "_layout");
        deleted = Field<HashSet<string>>(window, "_deletedExtrusions");
        Require(working.ColumnCount == 2 && deleted.Count == 0, "Undo did not restore the pre-Add-V topology.");
    }

    private static PanelCladdingEditorWindow BuildWindow(PanelCladdingLayout layout)
    {
        var repository = new StubRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(repository, new StubWorkbookRepository(), renderer, signature);
        var controller = new PanelCladdingEditorController(repository, save, renderer, signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1200d,
            Height = 900d,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        OperationResponse loaded = window.LoadPanel(layout.DocumentPath, layout.ObjectId);
        Require(loaded.Success, $"Fixture failed to load: {loaded.Message}");
        return window;
    }

    private static PanelCladdingLayout BuildLayout(
        int columns,
        int rows,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        var cells = new List<PanelCladdingCell>();
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
                string label = $"{column}{rowLabel}";
                string key = Key(column, rowLabel);
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabel,
                    ShortLabel = label,
                    UserTextKey = key,
                    Value = overrides?.GetValueOrDefault(key) ?? $"MAT-{label}"
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.NewGuid(),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/test/panel-cell-topology.3dm",
            ObjectName = "PID-TOPOLOGY",
            LayerFullPath = "CLDG::TEST",
            SystemCode = "CW01",
            GeometryFingerprint = "panel-cell-topology-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar fixture.",
            Width = columns * 10d,
            Height = rows * 10d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = Enumerable.Range(1, rows - 1).Select(index => index * 10d).ToArray(),
            VerticalOffsets = Enumerable.Range(1, columns - 1).Select(index => index * 10d).ToArray(),
            Cells = cells,
            WorkbookPath = string.Empty
        };
    }

    private static string Key(int column, string rowLabel) => PanelCladdingKeyService.GetCellKey(column, rowLabel);

    private static T Required<T>(T? value, string message) where T : class =>
        value ?? throw new InvalidOperationException(message);

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing test field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
            ?? throw new InvalidOperationException($"Missing test method {name} with {arguments.Length} parameter(s).");
        method.Invoke(instance, arguments);
    }

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260818_TEST_panel-cladding-cell-topology", fileName));

    private static void Render(FrameworkElement root, int width, int height, string outputPath)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0d, 0d, width, height));
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
        Require(new FileInfo(outputPath).Length > 20_000L, $"Rendered topology image is unexpectedly empty: {outputPath}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
            {
                yield return typed;
            }
            foreach (T descendant in Descendants<T>(child))
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

    private sealed class StubRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by topology smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used by topology smoke.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by topology smoke.");
    }

    private sealed class StubWorkbookRepository : IPanelCladdingWorkbookRepository
    {
        public OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail("Not used by topology smoke.");

        public OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
            PanelCladdingWorkbookBatchUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail("Not used by topology smoke.");
    }
}
