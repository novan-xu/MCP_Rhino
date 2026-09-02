using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingLatestUiSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildLayout();
        var repository = new StubRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var save = new PanelCladdingSaveService(repository, new StubWorkbookRepository(), renderer, signature);
        var controller = new PanelCladdingEditorController(repository, save, renderer, signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1440d,
            Height = 900d,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        OperationResponse loaded = window.LoadPanel(layout.DocumentPath, layout.ObjectId);
        Require(loaded.Success, $"Sample layout failed to load: {loaded.Message}");

        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 1440, 900);
        List<Button> buttons = Descendants<Button>(root).ToList();
        string[] requiredActions =
        [
            "Extrusion view", "Cladding view", "Clear assignment", "Merge", "Explode",
            "Hide", "Add H", "Add V", "Delete", "Material", "Parent cell",
            "Save Extrusions", "Save Cladding", "Save Both"
        ];
        foreach (string action in requiredActions)
        {
            Require(buttons.Any(button => string.Equals(button.Content?.ToString(), action, StringComparison.Ordinal)),
                $"Latest-design action is missing: {action}");
        }
        Require(!buttons.Any(button => string.Equals(button.Content?.ToString(), "Exit", StringComparison.Ordinal)),
            "The retired Exit action is still visible.");
        Require(!typeof(PanelCladdingEditorWindow).Assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, "Eto", StringComparison.OrdinalIgnoreCase)),
            "The latest editor assembly must not reference Eto.");
        Require(typeof(PanelCladdingEditorWindow).BaseType == typeof(Window),
            "PanelCladdingEditorWindow must remain a WPF Window.");
        Require(!typeof(PanelCladdingEditorWindow).Assembly.GetReferencedAssemblies()
                .Any(reference => reference.Name?.Contains("WebView", StringComparison.OrdinalIgnoreCase) == true),
            "The latest editor must not embed a web view.");

        Render(root, 1440, 900, OutputPath("panel-cladding-latest-cladding-1440x900.png"));

        Button extrusionButton = buttons.Single(button => string.Equals(button.Content?.ToString(), "Extrusion view", StringComparison.Ordinal));
        extrusionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(root, 1440, 900);
        Field<Border>(window, "ToastBorder").Visibility = Visibility.Collapsed;
        PanelCladdingGridCanvas canvas = Descendants<PanelCladdingGridCanvas>(root).Single();
        Require(canvas.ActiveView == PanelEditorView.Extrusion, "Extrusion view button did not activate the extrusion workspace.");
        Require(canvas.VisibleExtrusions.Count == 21, "The 4 x 3 fixture should render 4 frames and 17 atomic divider segments.");
        Button addHorizontalButton = buttons.Single(button => string.Equals(button.Content?.ToString(), "Add H", StringComparison.Ordinal));
        Require(addHorizontalButton.Visibility == Visibility.Visible,
            "Latest extrusion action shelf is not visible in extrusion mode.");
        addHorizontalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Equals(addHorizontalButton.Tag, true) && canvas.Cursor == Cursors.Cross,
            "Add H did not enter the latest cell-placement mode.");
        addHorizontalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(!Equals(addHorizontalButton.Tag, true), "Add H placement mode did not toggle off.");
        Field<Border>(window, "ToastBorder").Visibility = Visibility.Collapsed;
        Render(root, 1440, 900, OutputPath("panel-cladding-latest-extrusion-1440x900.png"));
        Render(root, 1024, 768, OutputPath("panel-cladding-latest-extrusion-1024x768.png"));

        PanelExtrusionSegment[] firstHorizontalRun = canvas.VisibleExtrusions
            .Where(item => !item.IsFrame && item.Axis == PanelExtrusionAxis.Horizontal)
            .GroupBy(item => Math.Round(item.Offset, 5))
            .First()
            .OrderBy(item => item.Start)
            .Take(2)
            .ToArray();
        HashSet<string> selectedExtrusions = Field<HashSet<string>>(window, "_selectedExtrusions");
        selectedExtrusions.Clear();
        selectedExtrusions.UnionWith(firstHorizontalRun.Select(item => item.Id));
        canvas.SetExtrusionState(PanelEditorView.Extrusion, selectedExtrusions,
            Field<List<PanelExtrusionMergeGroup>>(window, "_mergedGroups"),
            Field<HashSet<string>>(window, "_deletedExtrusions"));
        buttons.Single(button => string.Equals(button.Content?.ToString(), "Merge", StringComparison.Ordinal))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(root, 1440, 900);
        Require(canvas.VisibleExtrusions.Any(item => item.IsMerged), "Merge did not produce a continuous extrusion run.");
        Render(root, 1440, 900, OutputPath("panel-cladding-latest-merged-1440x900.png"));

        Invoke(window, "UndoLastChange");
        Layout(root, 1440, 900);
        Require(!canvas.VisibleExtrusions.Any(item => item.IsMerged), "Undo did not restore the atomic extrusion run.");

        int horizontalBefore = Field<List<double>>(window, "_horizontalOffsets").Count;
        Invoke(window, "AddMullion", PanelExtrusionAxis.Horizontal, 92.5d);
        Layout(root, 1440, 900);
        Require(Field<List<double>>(window, "_horizontalOffsets").Count == horizontalBefore + 1,
            "Add H did not create a session mullion.");

        var mullionDialog = new PanelNumericEditorDialog(
            "Add horizontal mullion",
            "Enter its orthogonal distance from the lower-left panel corner.",
            "New H offset from lower-left",
            88d,
            0d,
            112.6d,
            "Add mullion",
            exclusiveBounds: true);
        Render((FrameworkElement)mullionDialog.Content, 430, 272,
            OutputPath("panel-cladding-latest-mullion-dialog-430x272.png"));

        Console.WriteLine("[OK] Latest panel-cladding-editor.html action model is present in WPF.");
        Console.WriteLine("[OK] WPF window has no Eto or WebView dependency.");
        Console.WriteLine("[OK] Extrusion workspace renders 4 frames plus 17 fixture divider segments.");
        Console.WriteLine("[OK] Merge, undo, and add-mullion session behaviors passed.");
        Console.WriteLine("[OK] Desktop, compact, merged, and mullion-dialog renders were generated.");
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing test field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length)
            ?? throw new InvalidOperationException($"Missing test method {name}.");
        method.Invoke(instance, arguments);
    }

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260813_TEST_panel-cladding-latest-ui", fileName));

    private static void Layout(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0d, 0d, width, height));
        root.UpdateLayout();
    }

    private static void Render(FrameworkElement root, int width, int height, string outputPath)
    {
        Layout(root, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
        long minimumBytes = width * height < 200_000 ? 10_000L : 15_000L;
        Require(new FileInfo(outputPath).Length > minimumBytes, $"Off-screen UI snapshot is unexpectedly empty: {outputPath}");
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

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rowLabels = ["A", "B", "C"];
        string?[,] values =
        {
            { "TER-01", "0A", "MET-01", null },
            { "MET-01", "GLS-02", "GLS-02", "STN-01" },
            { "MET-01", "0C", "GLS-02", "STN-01" }
        };
        var cells = new List<PanelCladdingCell>();
        for (int row = 0; row < rowLabels.Length; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                string label = $"{column}{rowLabels[row]}";
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabels[row],
                    ShortLabel = label,
                    UserTextKey = PanelCladdingKeyService.GetCellKey(column, rowLabels[row]),
                    Value = values[row, column] ?? string.Empty
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000001"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/design/panel-cladding.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "CLDG::L03",
            SystemCode = "CW01",
            GeometryFingerprint = "latest-design-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar Brep is projectable.",
            Width = 181.1d,
            Height = 112.6d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [29.9d, 76.4d],
            VerticalOffsets = [49.2d, 82.7d, 142.5d],
            Cells = cells,
            WorkbookPath = "C:/design/panel-cladding-types.xlsx"
        };
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
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used by latest UI smoke.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used by latest UI smoke.");

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used by latest UI smoke.");
    }

    private sealed class StubWorkbookRepository : IPanelCladdingWorkbookRepository
    {
        public OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail("Not used by latest UI smoke.");

        public OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
            PanelCladdingWorkbookBatchUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail("Not used by latest UI smoke.");
    }
}
