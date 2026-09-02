using System.Collections;
using System.IO;
using System.Reflection;
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

namespace PanelCladdingDirectInteractionsSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildLayout();
        var repository = new CapturingRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var controller = new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signature),
            renderer,
            signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1440,
            Height = 900,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        Require(window.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Fixture failed to load.");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 1440, 900);
        var canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");
        DrawCanvas(canvas);

        Require(canvas.Background is SolidColorBrush { Color.A: 0 },
            "Canvas does not have an explicit transparent hit-test background.");
        string firstKey = layout.Cells[0].UserTextKey;
        canvas.SetSelection([firstKey]);
        Invoke(canvas, "ApplySelection", Array.Empty<string>(), ModifierKeys.Shift);
        Require(Field<HashSet<string>>(canvas, "_selectedKeys").Count == 0,
            "Blank cladding click did not clear selection.");
        canvas.SetExtrusionState(PanelEditorView.Extrusion, ["selected-extrusion"], [], new HashSet<string>());
        Invoke(canvas, "ApplySelection", Array.Empty<string>(), ModifierKeys.Control);
        Require(Field<HashSet<string>>(canvas, "_selectedExtrusions").Count == 0,
            "Blank extrusion click did not clear selection.");

        canvas.SetExtrusionState(PanelEditorView.Cladding, [], [], new HashSet<string>());
        HashSet<string> selectedKeys = Field<HashSet<string>>(window, "_selectedKeys");
        selectedKeys.Clear();
        selectedKeys.Add(firstKey);
        Invoke(window, "UpdateSelectionUi", true);
        var materialSelect = Named<ComboBox>(window, "MaterialSelect");
        PanelCladdingMaterial nextMaterial = ((IEnumerable)materialSelect.ItemsSource)
            .Cast<PanelCladdingMaterial>()
            .First(material => material.Code == "STN-003");
        materialSelect.SelectedItem = nextMaterial;
        Dictionary<string, string> values = Field<Dictionary<string, string>>(window, "_values");
        Require(values[firstKey] == "STN-003", "Material dropdown did not update the preview assignment immediately.");
        Require(repository.CommitCount == 0, "Material preview unexpectedly committed Rhino attributes.");

        Named<Button>(window, "ParentTabButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var parentSelect = Named<ComboBox>(window, "ParentReferenceSelect");
        PanelCladdingCell[] parentOptions = ((IEnumerable)parentSelect.ItemsSource).Cast<PanelCladdingCell>().ToArray();
        Require(parentOptions.Length == layout.Cells.Count - 1 && parentOptions.All(cell => cell.UserTextKey != firstKey),
            "Parent dropdown did not exclude the selected cell.");
        PanelCladdingCell parent = parentOptions.First();
        parentSelect.SelectedItem = parent;
        Require(values[firstKey] == parent.ShortLabel, "Parent dropdown did not update the preview immediately.");
        Require(repository.CommitCount == 0, "Parent preview unexpectedly committed Rhino attributes.");
        Require(window.FindName("ApplyMaterialButton") is null && window.FindName("ApplyParentButton") is null,
            "Assignment confirmation buttons remain in the UI.");

        var offsetList = Named<ItemsControl>(window, "DividerOffsetList");
        Layout(root, 1440, 900);
        TextBox firstOffsetEditor = Descendants<TextBox>(offsetList).First();
        Require(firstOffsetEditor.Tag is PanelOffsetDisplay { Code: "H0" },
            "Current Panel offset is not an editable field.");
        PanelOffsetDisplay offset = (PanelOffsetDisplay)firstOffsetEditor.Tag;
        firstOffsetEditor.Text = "31.25000 in";
        Invoke(window, "CommitDividerOffset", firstOffsetEditor, offset);
        List<double> horizontalOffsets = Field<List<double>>(window, "_horizontalOffsets");
        Require(Math.Abs(horizontalOffsets[0] - 31.25d) < 1e-8,
            "Editable Current Panel divider offset did not commit.");
        Require(repository.CommitCount == 0, "Divider preview unexpectedly committed Rhino attributes.");

        canvas.SetZoom(1d);
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent
        };
        Invoke(canvas, "OnPreviewMouseWheel", canvas, wheel);
        Require(Math.Abs(canvas.Zoom - 1.05d) < 1e-9,
            "One positive wheel step did not increase zoom by exactly 5%.");
        canvas.SetZoom(0.1d);
        canvas.PanBy(new Vector(10_000, 10_000));
        Require(Math.Abs(canvas.Zoom - 0.8d) < 1e-9 && canvas.PanOffset.Length < 1e-9,
            "80% minimum zoom does not collapse pan to the centered baseline.");
        canvas.SetZoom(2d);
        canvas.PanBy(new Vector(10_000, 10_000));
        Vector positiveLimit = canvas.PanOffset;
        Require(Math.Abs(canvas.Zoom - 1.5d) < 1e-9 && positiveLimit.X > 0 && positiveLimit.Y > 0,
            "150% maximum zoom or positive pan bound is incorrect.");
        canvas.PanBy(new Vector(-20_000, -20_000));
        Require(Math.Abs(canvas.PanOffset.X + positiveLimit.X) < 1e-6 &&
                Math.Abs(canvas.PanOffset.Y + positiveLimit.Y) < 1e-6,
            "Pan bounds are not symmetric within the 80% view envelope.");
        Require(PanelCladdingGridCanvas.ZoomStep == 0.05d,
            "Wheel zoom step is not exactly 5%. ");

        canvas.SetZoom(PanelCladdingGridCanvas.MinimumZoom);
        canvas.SetZoom(1d);
        DrawCanvas(canvas);
        Rect panelBounds = (Rect)InvokeResult(canvas, "CalculatePanelBounds", layout)!;
        Dictionary<(PanelDimensionAxis Axis, int Index), Rect> locks =
            Field<Dictionary<(PanelDimensionAxis Axis, int Index), Rect>>(canvas, "_dimensionLocks");
        Dictionary<(PanelDimensionAxis Axis, int Index), Rect> badges =
            Field<Dictionary<(PanelDimensionAxis Axis, int Index), Rect>>(canvas, "_dimensionBadges");
        Rect columnLock = locks[(PanelDimensionAxis.Column, 0)];
        Rect columnLabel = badges[(PanelDimensionAxis.Column, 0)];
        double dimensionLineY = panelBounds.Top - PanelCladdingGridCanvas.DimensionLineOffset;
        Require(Math.Abs(columnLock.Top + columnLock.Height / 2d - dimensionLineY) < 0.01d,
            "Dimension lock is not centered over the dimension line.");
        Require(Math.Abs(columnLabel.Top + columnLabel.Height / 2d - (dimensionLineY - 27d)) < 0.01d,
            "Dimension value did not retain its centered label position.");

        var zoomCombo = Named<ComboBox>(window, "ZoomCombo");
        Button clear = Named<Button>(window, "ClearCellsButton");
        StackPanel toolbarActions = Named<StackPanel>(window, "ToolbarActions");
        Require(toolbarActions.HorizontalAlignment == HorizontalAlignment.Right &&
                toolbarActions.Children.Count == 2 &&
                ReferenceEquals(toolbarActions.Children[0], clear) &&
                ReferenceEquals(toolbarActions.Children[1], zoomCombo),
            "Clear Assignment and zoom are not ordered in the far-right toolbar group.");
        clear.ApplyTemplate();
        Require(clear.Foreground is SolidColorBrush { Color: var foreground } && foreground.R > 150 && foreground.G < 100 &&
                clear.BorderBrush is SolidColorBrush { Color: var border } && border.R > 180 && border.G < 190,
            "Clear Assignment does not use the red-outline danger style.");
        Button saveExtrusions = Named<Button>(window, "SaveExtrusionsButton");
        Button saveCladding = Named<Button>(window, "SaveCladdingButton");
        Button save = Named<Button>(window, "SaveButton");
        Require(Equals(saveExtrusions.Content, "Save Extrusions") &&
                Equals(saveCladding.Content, "Save Cladding") &&
                Equals(save.Content, "Save Both") &&
                !Descendants<Button>(root).Any(button => Equals(button.Content, "Exit")) &&
                Math.Abs(saveExtrusions.ActualWidth - saveCladding.ActualWidth) <= 1d &&
                Math.Abs(saveCladding.ActualWidth - save.ActualWidth) <= 1d,
            "Footer does not expose equal-width scoped save actions without Exit.");

        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(source.Contains("254d / _layout.ModelUnitScaleToMillimeters", StringComparison.Ordinal),
            "Canvas grid is not based on ten physical inches.");
        Require(!source.Contains("Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)", StringComparison.Ordinal) &&
                source.Contains("MouseRightButtonDown += OnMouseRightButtonDown", StringComparison.Ordinal),
            "Unmodified right-button pan input is missing.");
        Require(source.Contains("segment.IsFrame ? PanelFrameStroke : PanelDividerStroke", StringComparison.Ordinal) &&
                source.Contains("double leftInset = cell.Column == 0 ? PanelFrameStroke : PanelDividerStroke / 2d", StringComparison.Ordinal),
            "Cladding and extrusion line weights were not halved and aligned.");
        int badgeMethodStart = source.IndexOf("private void DrawDimensionBadge", StringComparison.Ordinal);
        int cellMethodStart = source.IndexOf("private void DrawCell", badgeMethodStart, StringComparison.Ordinal);
        string badgeMethod = source[badgeMethodStart..cellMethodStart];
        Require(!badgeMethod.Contains("DrawRoundedRectangle", StringComparison.Ordinal),
            "Dimension value/lock container box is still rendered.");

        Render(root, 1440, 900, OutputPath("panel-cladding-direct-cladding-1440x900.png"));
        Named<Button>(window, "ExtrusionViewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(root, 1440, 900);
        Render(root, 1440, 900, OutputPath("panel-cladding-direct-extrusion-1440x900.png"));

        Console.WriteLine("[OK] Blank-space deselection passed in cladding and extrusion views.");
        Console.WriteLine("[OK] Direct material/parent assignment and editable divider preview remain Rhino-write-free.");
        Console.WriteLine("[OK] 80-150% bounded zoom, symmetric pan envelope, and 5% step passed.");
        Console.WriteLine("[OK] Dimension locks, far-right danger/zoom toolbar, footer, 10-inch grid, and half-weight strokes passed.");
    }

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rows = ["A", "B", "C", "D"];
        string[,] assignments =
        {
            { "GLS-004", "GLS-005" },
            { "STN-003", "STN-003" },
            { "TER-001", "TER-001" },
            { "TER-001", "TER-001" }
        };
        var cells = new List<PanelCladdingCell>();
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                string label = $"{column}{rows[row]}";
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rows[row],
                    ShortLabel = label,
                    UserTextKey = PanelCladdingKeyService.GetCellKey(column, rows[row]),
                    Value = assignments[row, column]
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000026"),
            DocumentRuntimeSerialNumber = 26,
            DocumentPath = "C:/design/direct-interactions.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "CW01",
            GeometryFingerprint = "direct-interactions-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [29.9d, 76.4d, 108.1d],
            VerticalOffsets = [45d],
            Cells = cells
        };
    }

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments) =>
        _ = InvokeResult(instance, name, arguments);

    private static object? InvokeResult(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(instance, arguments);
    }

    private static void Layout(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static void DrawCanvas(PanelCladdingGridCanvas canvas)
    {
        var visual = new DrawingVisual();
        using DrawingContext context = visual.RenderOpen();
        Invoke(canvas, "OnRender", context);
    }

    private static void Render(FrameworkElement root, int width, int height, string path)
    {
        Layout(root, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 20_000, $"Render is unexpectedly empty: {path}");
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

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260813_TEST_panel-cladding-direct-interactions", fileName));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public int CommitCount { get; private set; }

        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not required.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit)
        {
            CommitCount++;
            return OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
            {
                ObjectId = request.ObjectId,
                Mutated = true
            });
        }

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Ok(workbookPath);
    }
}
