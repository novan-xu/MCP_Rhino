using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.UI;

namespace PanelCladdingWheelZoomAspectSmoke;

internal static class Program
{
    private const int CanvasWidth = 1000;
    private const int CanvasHeight = 600;

    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildTallLayout();
        var canvas = new PanelCladdingGridCanvas
        {
            Width = CanvasWidth,
            Height = CanvasHeight
        };
        canvas.SetLayout(
            layout,
            layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value),
            new Dictionary<string, PanelCladdingMaterial>(),
            Array.Empty<string>());
        Layout(canvas);

        ReproduceOldIndependentClamp(layout);
        VerifyUniformZoom(canvas, layout);
        VerifyWheelInput(canvas, layout);
        VerifyHitAndViewGeometry(canvas, layout);
        VerifySourceContract();

        Console.WriteLine("[OK] The old independent width/height floor reproduces E1_05_48 stretching.");
        Console.WriteLine("[OK] Corrected 80-150% bounds preserve the model aspect ratio and scale both axes equally.");
        Console.WriteLine("[OK] Positive and negative wheel input uniformly zoom by one 5% step and remains handled.");
        Console.WriteLine("[OK] Cladding hit geometry and extrusion view share the corrected panel bounds.");
    }

    private static void ReproduceOldIndependentClamp(PanelCladdingLayout layout)
    {
        Size low = OldPanelSize(layout, PanelCladdingGridCanvas.MinimumZoom);
        Size high = OldPanelSize(layout, PanelCladdingGridCanvas.MaximumZoom);
        double modelRatio = layout.Width / layout.Height;
        Require(Math.Abs(low.Width - 80d) < 1e-9d && Math.Abs(high.Width - 80d) < 1e-9d,
            "The fixture no longer reaches the old frozen 80-DIP width branch.");
        Require(high.Height > low.Height * 1.8d,
            "The fixture did not reproduce height-only growth under the old formula.");
        Require(Math.Abs(low.Width / low.Height - modelRatio) > 0.1d &&
                Math.Abs(high.Width / high.Height - modelRatio) > 0.04d,
            "The old formula unexpectedly retained the tall panel's aspect ratio.");
    }

    private static void VerifyUniformZoom(PanelCladdingGridCanvas canvas, PanelCladdingLayout layout)
    {
        canvas.SetZoom(PanelCladdingGridCanvas.MinimumZoom);
        DrawCanvas(canvas);
        Rect low = PanelBounds(canvas, layout);
        Render(canvas, OutputPath("panel-E1_05_48-zoom-80.png"));

        canvas.SetZoom(1d);
        DrawCanvas(canvas);
        Rect normal = PanelBounds(canvas, layout);

        canvas.SetZoom(PanelCladdingGridCanvas.MaximumZoom);
        DrawCanvas(canvas);
        Rect high = PanelBounds(canvas, layout);
        Render(canvas, OutputPath("panel-E1_05_48-zoom-150.png"));

        double modelRatio = layout.Width / layout.Height;
        foreach ((string label, Rect bounds) in new[]
        {
            ("80%", low), ("100%", normal), ("150%", high)
        })
        {
            Require(Math.Abs(bounds.Width / bounds.Height - modelRatio) < 1e-10d,
                $"{label} preview ratio {bounds.Width / bounds.Height:G17} differs from model ratio {modelRatio:G17}.");
            Require(Math.Abs(bounds.Left + bounds.Width / 2d - canvas.ActualWidth / 2d) < 1e-9d &&
                    Math.Abs(bounds.Top + bounds.Height / 2d - canvas.ActualHeight / 2d) < 1e-9d,
                $"{label} preview is not centered at zero pan.");
        }

        double expectedFactor = PanelCladdingGridCanvas.MaximumZoom / PanelCladdingGridCanvas.MinimumZoom;
        double widthFactor = high.Width / low.Width;
        double heightFactor = high.Height / low.Height;
        Require(high.Width > low.Width && high.Height > low.Height,
            "Both panel dimensions must increase with zoom.");
        Require(Math.Abs(widthFactor - expectedFactor) < 1e-10d &&
                Math.Abs(heightFactor - expectedFactor) < 1e-10d &&
                Math.Abs(widthFactor - heightFactor) < 1e-10d,
            "80-150% zoom did not apply one identical scale factor to both dimensions.");
    }

    private static void VerifyWheelInput(PanelCladdingGridCanvas canvas, PanelCladdingLayout layout)
    {
        canvas.SetZoom(1d);
        DrawCanvas(canvas);
        Rect before = PanelBounds(canvas, layout);
        MouseWheelEventArgs positive = Wheel(120);
        Invoke(canvas, "OnPreviewMouseWheel", canvas, positive);
        DrawCanvas(canvas);
        Rect after = PanelBounds(canvas, layout);

        Require(positive.Handled, "Positive wheel input was not marked handled.");
        Require(Math.Abs(canvas.Zoom - 1.05d) < 1e-9d,
            "One positive wheel detent did not increase zoom from 100% to 105%.");
        Require(Math.Abs(after.Width / before.Width - 1.05d) < 1e-10d &&
                Math.Abs(after.Height / before.Height - 1.05d) < 1e-10d,
            "Positive wheel input did not uniformly scale both preview axes.");

        MouseWheelEventArgs negative = Wheel(-120);
        Invoke(canvas, "OnPreviewMouseWheel", canvas, negative);
        DrawCanvas(canvas);
        Rect restored = PanelBounds(canvas, layout);
        Require(negative.Handled && Math.Abs(canvas.Zoom - 1d) < 1e-9d,
            "One negative wheel detent did not return zoom to 100%.");
        Require(RectClose(before, restored),
            "Positive then negative wheel input did not restore the original panel bounds.");
    }

    private static void VerifyHitAndViewGeometry(PanelCladdingGridCanvas canvas, PanelCladdingLayout layout)
    {
        canvas.SetZoom(PanelCladdingGridCanvas.MinimumZoom);
        canvas.SetExtrusionState(
            PanelEditorView.Cladding,
            Array.Empty<string>(),
            Array.Empty<PanelExtrusionMergeGroup>(),
            new HashSet<string>());
        DrawCanvas(canvas);
        Rect lowPanel = PanelBounds(canvas, layout);
        Rect lowCell = CellRects(canvas).Values.First();

        canvas.SetZoom(PanelCladdingGridCanvas.MaximumZoom);
        DrawCanvas(canvas);
        Rect highPanel = PanelBounds(canvas, layout);
        Rect highCell = CellRects(canvas).Values.First();
        Require(highCell.Width > lowCell.Width && highCell.Height > lowCell.Height,
            "Cell hit rectangles did not grow on both axes with the corrected preview.");
        Require(highCell.Left >= highPanel.Left && highCell.Right <= highPanel.Right &&
                highCell.Top >= highPanel.Top && highCell.Bottom <= highPanel.Bottom,
            "Cladding hit geometry is not contained by the corrected panel bounds.");

        canvas.SetExtrusionState(
            PanelEditorView.Extrusion,
            Array.Empty<string>(),
            Array.Empty<PanelExtrusionMergeGroup>(),
            new HashSet<string>());
        DrawCanvas(canvas);
        Rect extrusionPanel = PanelBounds(canvas, layout);
        Require(RectClose(highPanel, extrusionPanel),
            "Switching to extrusion view changed the corrected preview bounds.");
    }

    private static void VerifySourceContract()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(!source.Contains("Math.Max(80d, baseWidth * zoom)", StringComparison.Ordinal) &&
                !source.Contains("Math.Max(60d, baseWidth * zoom / ratio)", StringComparison.Ordinal),
            "Independent post-zoom width/height floors remain in the preview size calculation.");
        Require(source.Contains("return new Size(scaledWidth, scaledWidth / ratio);", StringComparison.Ordinal),
            "Preview size is not derived from one shared scaled width and model ratio.");
    }

    private static PanelCladdingLayout BuildTallLayout()
    {
        double[] offsets = { 22.875d, 108.75d, 170.59079d };
        string[] rows = { "A", "B", "C", "D" };
        var values = new[] { "GLS-005", "GLS-005", "TER-001", "TER-001" };
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("E1054800-0000-0000-0000-000000000001"),
            DocumentPath = "E1_05_48.3dm",
            Width = 18.75d,
            Height = 199.5d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = offsets,
            VerticalOffsets = Array.Empty<double>(),
            Cells = rows.Select((row, index) => new PanelCladdingCell
            {
                Column = 0,
                Row = index,
                RowLabel = row,
                ShortLabel = $"0{row}",
                UserTextKey = PanelCladdingKeyService.GetCellKey(0, row),
                Value = values[index]
            }).ToArray()
        };
    }

    private static Size OldPanelSize(PanelCladdingLayout layout, double zoom)
    {
        double availableWidth = Math.Max(220d, CanvasWidth - 150d * 2d);
        double availableHeight = Math.Max(160d, CanvasHeight - 110d * 2d);
        double ratio = layout.Width / layout.Height;
        double baseWidth = Math.Min(availableWidth, availableHeight * ratio);
        return new Size(
            Math.Max(80d, baseWidth * zoom),
            Math.Max(60d, baseWidth * zoom / ratio));
    }

    private static MouseWheelEventArgs Wheel(int delta) => new(Mouse.PrimaryDevice, Environment.TickCount, delta)
    {
        RoutedEvent = UIElement.PreviewMouseWheelEvent
    };

    private static Rect PanelBounds(PanelCladdingGridCanvas canvas, PanelCladdingLayout layout) =>
        (Rect)(InvokeResult(canvas, "CalculatePanelBounds", layout) ??
            throw new InvalidOperationException("Panel bounds were not returned."));

    private static Dictionary<string, Rect> CellRects(PanelCladdingGridCanvas canvas) =>
        (Dictionary<string, Rect>)(typeof(PanelCladdingGridCanvas)
            .GetField("_cellRects", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(canvas) ?? throw new InvalidOperationException("Cell rectangles are unavailable."));

    private static bool RectClose(Rect left, Rect right) =>
        Math.Abs(left.Left - right.Left) < 1e-9d &&
        Math.Abs(left.Top - right.Top) < 1e-9d &&
        Math.Abs(left.Width - right.Width) < 1e-9d &&
        Math.Abs(left.Height - right.Height) < 1e-9d;

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(CanvasWidth, CanvasHeight));
        element.Arrange(new Rect(0d, 0d, CanvasWidth, CanvasHeight));
        element.UpdateLayout();
    }

    private static void DrawCanvas(PanelCladdingGridCanvas canvas)
    {
        var visual = new DrawingVisual();
        using DrawingContext context = visual.RenderOpen();
        _ = InvokeResult(canvas, "OnRender", context);
    }

    private static void Render(FrameworkElement element, string path)
    {
        Layout(element);
        var bitmap = new RenderTargetBitmap(CanvasWidth * 2, CanvasHeight * 2, 192d, 192d, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 15_000, $"Render is unexpectedly empty: {path}");
    }

    private static object? InvokeResult(object target, string name, params object?[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) ??
            throw new InvalidOperationException($"Method is missing: {name}");
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static void Invoke(object target, string name, params object?[] arguments) =>
        _ = InvokeResult(target, name, arguments);

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

    private static string OutputPath(string name) => Path.Combine(
        FindRepositoryRoot(), "Project_Test", "260818_TEST_panel-cladding-wheel-zoom-aspect", name);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
