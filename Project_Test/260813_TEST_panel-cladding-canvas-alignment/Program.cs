using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingCanvasAlignmentSmoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildLayout();
        VerifyWindow(layout, 1440, 900, verifyToolbar: true);
        VerifyWindow(layout, 1024, 768, verifyToolbar: false);

        string xaml = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "PanelCladdingEditor", "UI", "PanelCladdingEditorWindow.xaml"));
        Require(!xaml.Contains("CanvasNotePanel", StringComparison.Ordinal) &&
                !xaml.Contains("Right drag pan", StringComparison.Ordinal) &&
                !xaml.Contains("Wheel zoom", StringComparison.Ordinal),
            "The obsolete bottom canvas instructions remain in XAML.");

        Console.WriteLine("[OK] Bottom canvas instructions are absent.");
        Console.WriteLine("[OK] Clear Assignment precedes zoom in the far-right toolbar group and matches Delete styling.");
        Console.WriteLine("[OK] Cladding and extrusion panels are centered on the actual canvas at 1440x900 and 1024x768.");
        Console.WriteLine("[OK] Notification remains canvas-centered and clear of extrusion actions at every zoom/pan state.");
    }

    private static void VerifyWindow(PanelCladdingLayout layout, int width, int height, bool verifyToolbar)
    {
        var repository = new FixtureRepository(layout);
        var keys = new PanelCladdingKeyService();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var controller = new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signature),
            new PanelPreviewRenderer(),
            signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = width,
            Height = height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        Require(window.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Fixture failed to load.");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, width, height);
        PanelCladdingGridCanvas canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");

        canvas.SetZoom(1d);
        DrawCanvas(canvas);
        Rect cladding = PanelBounds(canvas, layout);
        VerifyCentered(canvas, cladding, $"cladding {width}x{height}");
        Render(root, width, height, OutputPath($"panel-cladding-centered-{width}x{height}.png"));

        Named<Button>(window, "ExtrusionViewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(root, width, height);
        DrawCanvas(canvas);
        Rect extrusion = PanelBounds(canvas, layout);
        VerifyCentered(canvas, extrusion, $"extrusion {width}x{height}");
        Require(Math.Abs(cladding.Left - extrusion.Left) < 0.01d &&
                Math.Abs(cladding.Top - extrusion.Top) < 0.01d &&
                Math.Abs(cladding.Width - extrusion.Width) < 0.01d &&
                Math.Abs(cladding.Height - extrusion.Height) < 0.01d,
            $"Switching views changed the panel bounds at {width}x{height}.");
        Render(root, width, height, OutputPath($"panel-extrusion-centered-{width}x{height}.png"));
        VerifyNotification(window, root, canvas, width, height);

        if (verifyToolbar)
        {
            VerifyToolbar(window, root);
            Render(root, width, height, OutputPath("panel-extrusion-canvas-notification-1440x900.png"));
        }
    }

    private static void VerifyNotification(
        PanelCladdingEditorWindow window,
        FrameworkElement root,
        PanelCladdingGridCanvas canvas,
        int width,
        int height)
    {
        Grid workspace = Named<Grid>(window, "CanvasWorkspace");
        Border toast = Named<Border>(window, "ToastBorder");
        StackPanel actions = Named<StackPanel>(window, "ExtrusionActions");
        Require(ReferenceEquals(VisualTreeHelper.GetParent(toast), workspace),
            "Notification is not owned by the canvas workspace.");

        _ = InvokeResult(window, "ShowToast", "H0 added · new cladding cells are unassigned");
        Layout(root, width, height);
        Point toastTopLeft = toast.TranslatePoint(default, workspace);
        Point actionsTopLeft = actions.TranslatePoint(default, workspace);
        double center = toastTopLeft.X + toast.ActualWidth / 2d;
        double gap = actionsTopLeft.Y - (toastTopLeft.Y + toast.ActualHeight);
        Require(Math.Abs(center - workspace.ActualWidth / 2d) <= 0.51d,
            $"Notification is not centered on the canvas at {width}x{height}: " +
            $"center={center:0.###}, workspace={workspace.ActualWidth:0.###}.");
        Require(gap >= 12d,
            $"Notification overlaps or crowds the extrusion buttons at {width}x{height}: gap {gap:0.##}.");

        canvas.SetZoom(PanelCladdingGridCanvas.MaximumZoom);
        canvas.PanBy(new Vector(10_000d, -10_000d));
        DrawCanvas(canvas);
        Layout(root, width, height);
        Point movedToastTopLeft = toast.TranslatePoint(default, workspace);
        double movedCenter = movedToastTopLeft.X + toast.ActualWidth / 2d;
        Require(Math.Abs(movedCenter - center) < 0.01d,
            "Notification moved with the panned/zoomed panel instead of staying canvas-centered.");
        canvas.PanBy(-canvas.PanOffset);
        DrawCanvas(canvas);
        Layout(root, width, height);
    }

    private static void VerifyToolbar(PanelCladdingEditorWindow window, FrameworkElement root)
    {
        StackPanel actions = Named<StackPanel>(window, "ToolbarActions");
        Button clear = Named<Button>(window, "ClearCellsButton");
        ComboBox zoom = Named<ComboBox>(window, "ZoomCombo");
        Require(actions.HorizontalAlignment == HorizontalAlignment.Right &&
                actions.Children.Count == 2 &&
                ReferenceEquals(actions.Children[0], clear) &&
                ReferenceEquals(actions.Children[1], zoom),
            "Toolbar action ordering or right alignment is incorrect.");
        FrameworkElement toolbarGrid = (FrameworkElement)VisualTreeHelper.GetParent(actions);
        double actionRight = actions.TranslatePoint(new Point(actions.ActualWidth, 0d), toolbarGrid).X;
        Require(Math.Abs(actionRight - toolbarGrid.ActualWidth) < 0.1d,
            "Toolbar action group is not positioned at the far right.");

        clear.IsEnabled = true;
        clear.ApplyTemplate();
        Button delete = Named<Button>(window, "DeleteExtrusionsButton");
        delete.ApplyTemplate();
        Border clearBorder = Descendants<Border>(clear).Single(border => border.Name == "ButtonBorder");
        Border deleteBorder = Descendants<Border>(delete).Single(border => border.Name == "ButtonBorder");
        Require(SameColor(clear.Foreground, delete.Foreground) && SameColor(clearBorder.BorderBrush, deleteBorder.BorderBrush) &&
                clearBorder.CornerRadius == deleteBorder.CornerRadius,
            "Clear Assignment does not match the Delete danger-button visual contract.");
        Require(window.FindName("CanvasNotePanel") is null, "Canvas instruction panel still exists.");
    }

    private static bool SameColor(Brush left, Brush right) =>
        left is SolidColorBrush leftBrush && right is SolidColorBrush rightBrush && leftBrush.Color == rightBrush.Color;

    private static void VerifyCentered(PanelCladdingGridCanvas canvas, Rect panel, string label)
    {
        Require(canvas.PanOffset.Length < 1e-9d, $"{label} unexpectedly has a pan offset.");
        double deltaX = panel.Left + panel.Width / 2d - canvas.ActualWidth / 2d;
        double deltaY = panel.Top + panel.Height / 2d - canvas.ActualHeight / 2d;
        Require(Math.Abs(deltaX) < 0.01d && Math.Abs(deltaY) < 0.01d,
            $"Panel is not centered for {label}: delta ({deltaX:0.###}, {deltaY:0.###}).");
    }

    private static Rect PanelBounds(PanelCladdingGridCanvas canvas, PanelCladdingLayout layout) =>
        (Rect)(InvokeResult(canvas, "CalculatePanelBounds", layout)
            ?? throw new InvalidOperationException("Panel bounds were not returned."));

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rows = ["A", "B", "C", "D"];
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
                    Value = row == 2 && column == 1 ? "GLS-003" : string.Empty
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000031"),
            DocumentRuntimeSerialNumber = 31,
            DocumentPath = "C:/design/canvas-alignment.3dm",
            ObjectName = "W3_05_13",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "WT03",
            GeometryFingerprint = "canvas-alignment-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 198d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [21.375d, 107.25d, 169.09079d],
            VerticalOffsets = [45d],
            Cells = cells
        };
    }

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

    private static object? InvokeResult(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(instance, arguments);
    }

    private static void Layout(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0d, 0d, width, height));
        element.UpdateLayout();
    }

    private static void DrawCanvas(PanelCladdingGridCanvas canvas)
    {
        var visual = new DrawingVisual();
        using DrawingContext context = visual.RenderOpen();
        _ = InvokeResult(canvas, "OnRender", context);
    }

    private static void Render(FrameworkElement element, int width, int height, string path)
    {
        Layout(element, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(element);
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

    private static string OutputPath(string name) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260813_TEST_panel-cladding-canvas-alignment", name));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixtureRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
    {
        public OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingLayout>.Ok(layout);

        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId) =>
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not required.");

        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request,
            Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Ok(new PanelAttributeCommitResult
            {
                ObjectId = request.ObjectId,
                Mutated = true
            });

        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Ok(workbookPath);
    }
}
