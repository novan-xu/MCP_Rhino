using System.Collections;
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

namespace PanelCladdingExtrusionFidelitySmoke;

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
            Width = 1120,
            Height = 800,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        Require(window.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Fixture failed to load.");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 1120, 800);

        VerifyOffsets(window);
        PanelCladdingGridCanvas canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");
        canvas.SetExtrusionState(PanelEditorView.Cladding, [], [], new HashSet<string>());
        RenderTargetBitmap cladding = DrawCanvas(canvas);
        Rect panel = (Rect)InvokeResult(canvas, "CalculatePanelBounds", layout)!;
        int claddingFrame = DarkBandHorizontal(cladding, (int)Math.Round(panel.Left), (int)Math.Round(panel.Top + panel.Height * 0.25d));
        int claddingDivider = DarkBandHorizontal(
            cladding,
            (int)Math.Round(panel.Left + panel.Width * layout.VerticalOffsets[0] / layout.Width),
            (int)Math.Round(panel.Top + panel.Height * 0.12d));

        canvas.SetExtrusionState(PanelEditorView.Extrusion, [], [], new HashSet<string>());
        _ = DrawCanvas(canvas);
        PanelExtrusionSegment segment = canvas.VisibleExtrusions.First(item =>
            !item.IsFrame && item.Axis == PanelExtrusionAxis.Horizontal);
        RenderTargetBitmap extrusion = DrawCanvas(canvas);
        int extrusionFrame = DarkBandHorizontal(extrusion, (int)Math.Round(panel.Left), (int)Math.Round(panel.Top + panel.Height * 0.25d));
        int extrusionDivider = DarkBandHorizontal(
            extrusion,
            (int)Math.Round(panel.Left + panel.Width * layout.VerticalOffsets[0] / layout.Width),
            (int)Math.Round(panel.Top + panel.Height * 0.12d));
        Require(claddingFrame == extrusionFrame && claddingDivider == extrusionDivider,
            $"Cladding/extrusion stroke bands differ (frame {claddingFrame}/{extrusionFrame}, divider {claddingDivider}/{extrusionDivider}).");

        VerifyLabelCenters(canvas, panel, layout);
        canvas.SetExtrusionState(PanelEditorView.Extrusion, [segment.Id], [], new HashSet<string>());
        RenderTargetBitmap selected = DrawCanvas(canvas);
        VerifySelection(selected, panel, layout, segment);
        Render(root, 1120, 800, OutputPath("extrusion-fidelity-1120x800.png"));

        Console.WriteLine("[OK] Divider offsets are unit-free, five-decimal, and wide enough for XXX.XXXXX.");
        Console.WriteLine("[OK] Curve-name badge centers coincide with curve midpoints.");
        Console.WriteLine("[OK] Cladding and extrusion base frame/divider pixel bands are identical.");
        Console.WriteLine("[OK] Extrusion selection uses a visible yellow halo around the normal dark curve core.");
    }

    private static void VerifyOffsets(PanelCladdingEditorWindow window)
    {
        ItemsControl list = Named<ItemsControl>(window, "DividerOffsetList");
        PanelOffsetDisplay[] offsets = ((IEnumerable)list.ItemsSource).Cast<PanelOffsetDisplay>().ToArray();
        Require(offsets.All(item => item.Display == $"{item.Value:0.00000}" &&
                                    !item.Display.Contains(item.Unit, StringComparison.OrdinalIgnoreCase)),
            "Divider offsets still include a unit or lost five-decimal formatting.");
        TextBox[] editors = Descendants<TextBox>(list).ToArray();
        Require(editors.Length == offsets.Length && editors.All(editor => editor.ActualWidth >= 64d),
            "Divider offset editors do not reserve the required numeric width.");
        var probe = new FormattedText(
            "999.99999",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            10d,
            Brushes.Black,
            1d);
        Require(editors.All(editor => editor.ActualWidth - editor.Padding.Left - editor.Padding.Right >= probe.Width),
            "Divider offset editor cannot show XXX.XXXXX without clipping.");
    }

    private static void VerifyLabelCenters(PanelCladdingGridCanvas canvas, Rect panel, PanelCladdingLayout layout)
    {
        Dictionary<string, Rect> labels = Field<Dictionary<string, Rect>>(canvas, "_segmentLabelRects");
        foreach (PanelExtrusionSegment segment in canvas.VisibleExtrusions)
        {
            Rect label = labels[segment.Id];
            Point expected = segment.Axis == PanelExtrusionAxis.Horizontal
                ? new Point(
                    panel.Left + panel.Width * ((segment.Start + segment.End) / 2d) / layout.Width,
                    panel.Bottom - panel.Height * segment.Offset / layout.Height)
                : new Point(
                    panel.Left + panel.Width * segment.Offset / layout.Width,
                    panel.Bottom - panel.Height * ((segment.Start + segment.End) / 2d) / layout.Height);
            Require(Math.Abs(label.Left + label.Width / 2d - expected.X) < 0.01d &&
                    Math.Abs(label.Top + label.Height / 2d - expected.Y) < 0.01d,
                $"Curve badge {segment.Code} is not centered on its curve midpoint.");
        }
    }

    private static void VerifySelection(
        BitmapSource bitmap,
        Rect panel,
        PanelCladdingLayout layout,
        PanelExtrusionSegment segment)
    {
        int x = (int)Math.Round(panel.Left + panel.Width * (segment.Start + (segment.End - segment.Start) * 0.25d) / layout.Width);
        int y = (int)Math.Round(panel.Bottom - panel.Height * segment.Offset / layout.Height);
        Color core = Pixel(bitmap, x, y);
        Require(IsDark(core), $"Selected curve core no longer preserves the normal dark stroke: {core}.");
        int dark = 0;
        int yellow = 0;
        for (int sampleY = y - 8; sampleY <= y + 8; sampleY++)
        {
            Color color = Pixel(bitmap, x, sampleY);
            dark += IsDark(color) ? 1 : 0;
            yellow += IsYellow(color) ? 1 : 0;
        }
        Require(dark is >= 1 and <= 4 && yellow >= 2,
            $"Selected curve does not have a normal-weight dark core plus visible yellow halo ({dark}/{yellow}).");
    }

    private static int DarkBandHorizontal(BitmapSource bitmap, int centerX, int y)
    {
        int count = 0;
        for (int x = centerX - 8; x <= centerX + 8; x++)
        {
            if (IsDark(Pixel(bitmap, x, y)))
            {
                count++;
            }
        }
        return count;
    }

    private static bool IsDark(Color color) => color.R < 180 && color.G < 180 && color.B < 180 && !IsYellow(color);
    private static bool IsYellow(Color color) => color.R > 190 && color.G > 180 && color.B < 60;

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rows = ["A", "B", "C"];
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
                    Value = column == 0 ? "TER-001" : "STN-003"
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000030"),
            DocumentRuntimeSerialNumber = 30,
            DocumentPath = "C:/design/extrusion-fidelity.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "CW01",
            GeometryFingerprint = "extrusion-fidelity-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 198d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [28.90921d, 90.75d],
            VerticalOffsets = [123.45678d],
            Cells = cells
        };
    }

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static object? InvokeResult(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(instance, arguments);
    }

    private static void Layout(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(Math.Max(1, width), Math.Max(1, height)));
        element.Arrange(new Rect(0, 0, Math.Max(1, width), Math.Max(1, height)));
        element.UpdateLayout();
    }

    private static RenderTargetBitmap DrawCanvas(PanelCladdingGridCanvas canvas)
    {
        var visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            _ = InvokeResult(canvas, "OnRender", context);
        }
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(canvas.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(canvas.ActualHeight)),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void Render(FrameworkElement element, int width, int height, string path)
    {
        Layout(element, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 20_000, $"Render is unexpectedly empty: {path}");
    }

    private static Color Pixel(BitmapSource bitmap, int x, int y)
    {
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
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
        "Project_Test", "260813_TEST_panel-cladding-extrusion-fidelity", fileName));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class CapturingRepository(PanelCladdingLayout layout) : ILivePanelCladdingRepository
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
