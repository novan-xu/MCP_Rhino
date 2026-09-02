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

namespace PanelCladdingExtrusionVisualsSmoke;

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
        PanelCladdingGridCanvas canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");

        string selectedCell = layout.Cells[0].UserTextKey;
        canvas.SetExtrusionState(PanelEditorView.Cladding, [], [], new HashSet<string>());
        canvas.SetSelection([selectedCell]);
        RenderTargetBitmap claddingBitmap = DrawCanvas(canvas);
        Rect selectedRect = Field<Dictionary<string, Rect>>(canvas, "_cellRects")[selectedCell];
        Color selectedFill = Pixel(claddingBitmap,
            (int)Math.Round(selectedRect.Left + 12d),
            (int)Math.Round(selectedRect.Top + 12d));
        Require(selectedFill == Color.FromRgb(255, 234, 0),
            $"Cladding selection is not bright-yellow overwrite: {selectedFill}.");
        Render(root, 1440, 900, OutputPath("cladding-yellow-selection-1440x900.png"));

        Named<Button>(window, "ExtrusionViewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(root, 1440, 900);
        _ = DrawCanvas(canvas);
        PanelExtrusionSegment selectedSegment = canvas.VisibleExtrusions.First(segment =>
            !segment.IsFrame && segment.Axis == PanelExtrusionAxis.Horizontal);
        canvas.SetExtrusionState(PanelEditorView.Extrusion, [selectedSegment.Id], [], new HashSet<string>());
        RenderTargetBitmap extrusionBitmap = DrawCanvas(canvas);
        Rect panel = (Rect)InvokeResult(canvas, "CalculatePanelBounds", layout)!;
        VerifySelectedExtrusion(extrusionBitmap, panel, layout, selectedSegment);
        VerifyHollowIntersection(extrusionBitmap, panel, layout);
        VerifyDimensionClearance(canvas, panel);
        VerifyActionGroups(window);
        VerifySourceContracts();
        Render(root, 1440, 900, OutputPath("extrusion-yellow-selection-1440x900.png"));

        Console.WriteLine("[OK] Cladding overwrite and extrusion halo use bright-yellow selection feedback.");
        Console.WriteLine("[OK] Extrusion selection retains the shared 2/1.5-DIP line weights.");
        Console.WriteLine("[OK] Intersections are hollow and names use larger black midpoint labels.");
        Console.WriteLine("[OK] Dimensions use 60-DIP clearance and extrusion cells retain muted material codes.");
        Console.WriteLine("[OK] Structural actions are compact and arranged as two rounded groups.");
    }

    private static void VerifySelectedExtrusion(
        RenderTargetBitmap bitmap,
        Rect panel,
        PanelCladdingLayout layout,
        PanelExtrusionSegment segment)
    {
        double x1 = panel.Left + panel.Width * segment.Start / layout.Width;
        double x2 = panel.Left + panel.Width * segment.End / layout.Width;
        double y = panel.Bottom - panel.Height * segment.Offset / layout.Height;
        int sampleX = (int)Math.Round(x1 + (x2 - x1) * 0.25d);
        int sampleY = (int)Math.Round(y);
        Color center = Pixel(bitmap, sampleX, sampleY);
        Require(IsDark(center), $"Selected extrusion did not preserve its dark normal-weight core: {center}.");
        int darkCore = Enumerable.Range(sampleY - 7, 15)
            .Count(candidateY => IsDark(Pixel(bitmap, sampleX, candidateY)));
        int yellowHalo = Enumerable.Range(sampleY - 7, 15)
            .Count(candidateY => IsYellow(Pixel(bitmap, sampleX, candidateY)));
        Require(darkCore is >= 1 and <= 4 && yellowHalo >= 2,
            $"Selected extrusion core/halo is not developed correctly ({darkCore}/{yellowHalo}).");
    }

    private static void VerifyHollowIntersection(
        RenderTargetBitmap bitmap,
        Rect panel,
        PanelCladdingLayout layout)
    {
        int x = (int)Math.Round(panel.Left + panel.Width * layout.VerticalOffsets[0] / layout.Width);
        int y = (int)Math.Round(panel.Bottom - panel.Height * layout.HorizontalOffsets[0] / layout.Height);
        Color center = Pixel(bitmap, x, y);
        Require(center.R > 245 && center.G > 245 && center.B > 245,
            $"Intersection center is not hollow/white: {center}.");
        bool hasDarkRing = false;
        for (int dx = -5; dx <= 5; dx++)
        {
            for (int dy = -5; dy <= 5; dy++)
            {
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance >= 2.5d && distance <= 4.8d)
                {
                    Color sample = Pixel(bitmap, x + dx, y + dy);
                    hasDarkRing |= sample.R < 90 && sample.G < 100 && sample.B < 105;
                }
            }
        }
        Require(hasDarkRing, "Intersection has no dark hollow-dot contour.");
    }

    private static void VerifyDimensionClearance(PanelCladdingGridCanvas canvas, Rect panel)
    {
        Dictionary<(PanelDimensionAxis Axis, int Index), Rect> locks =
            Field<Dictionary<(PanelDimensionAxis Axis, int Index), Rect>>(canvas, "_dimensionLocks");
        Rect columnLock = locks[(PanelDimensionAxis.Column, 0)];
        Rect rowLock = locks[(PanelDimensionAxis.Row, 0)];
        Require(Math.Abs(columnLock.Top + columnLock.Height / 2d -
                         (panel.Top - PanelCladdingGridCanvas.DimensionLineOffset)) < 0.01d,
            "Column dimension system did not move to the 60-DIP line offset.");
        Require(Math.Abs(rowLock.Left + rowLock.Width / 2d -
                         (panel.Left - PanelCladdingGridCanvas.DimensionLineOffset)) < 0.01d,
            "Row dimension system did not move to the 60-DIP line offset.");
        Require(Math.Abs(PanelCladdingGridCanvas.DimensionLineOffset - 60d) < 0.01d,
            "Dimension line gap is not exactly triple the former 20-DIP offset.");
    }

    private static void VerifyActionGroups(PanelCladdingEditorWindow window)
    {
        Border mergeGroup = Named<Border>(window, "MergeActionGroup");
        Border editGroup = Named<Border>(window, "EditActionGroup");
        Require(!ReferenceEquals(mergeGroup, editGroup) && mergeGroup.CornerRadius.TopLeft == 7d &&
                editGroup.CornerRadius.TopLeft == 7d,
            "Structural actions are not split into two rounded groups.");
        Button[] buttons =
        [
            Named<Button>(window, "MergeExtrusionsButton"),
            Named<Button>(window, "ExplodeExtrusionsButton"),
            Named<Button>(window, "HideExtrusionsButton"),
            Named<Button>(window, "AddHorizontalMullionButton"),
            Named<Button>(window, "AddVerticalMullionButton"),
            Named<Button>(window, "DeleteExtrusionsButton")
        ];
        foreach (Button button in buttons)
        {
            button.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Require(Math.Abs(button.DesiredSize.Height - 32d) < 0.1d,
                $"{button.Name} is not a compact 32-DIP action ({button.DesiredSize.Height}).");
        }
        Require(buttons.Take(3).All(button => IsDescendantOf(button, mergeGroup)) &&
                buttons.Skip(3).All(button => IsDescendantOf(button, editGroup)),
            "Structural action membership does not match Merge/Explode/Hide and Add H/Add V/Delete groups.");
    }

    private static void VerifySourceContracts()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(source.Contains("PanelFrameStroke = 2d", StringComparison.Ordinal) &&
                source.Contains("PanelDividerStroke = 1.5d", StringComparison.Ordinal) &&
                source.Contains("segment.IsFrame ? PanelFrameStroke : PanelDividerStroke", StringComparison.Ordinal),
            "Cladding/extrusion shared stroke constants are missing.");
        Require(source.Contains("FrozenBrush(\"#FFEA00\")", StringComparison.Ordinal) &&
                source.Contains("SelectionHaloBandWidth = 2d", StringComparison.Ordinal) &&
                !source.Contains("segment.IsFrame ? 11d : 10d", StringComparison.Ordinal),
            "Yellow halo or normal-weight extrusion core contract is missing.");
        Require(source.Contains("Formatted(displayCode, 11d", StringComparison.Ordinal) &&
                source.Contains("midpoint.Y - labelHeight / 2d", StringComparison.Ordinal) &&
                source.Contains("ExtrusionLabelText = FrozenBrush(\"#000000\")", StringComparison.Ordinal),
            "Extrusion label size, black text, or midpoint-overlap placement is missing.");
        Require(source.Contains("dc.DrawEllipse(Surface, new Pen(GridLine, PanelDividerStroke)", StringComparison.Ordinal),
            "Hollow intersection rendering is missing.");
        Require(source.Contains("string displayMaterial", StringComparison.Ordinal) &&
                source.Contains("var ghostMaterial", StringComparison.Ordinal),
            "Extrusion cells do not retain a muted resolved material-code label.");
    }

    private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool IsYellow(Color color) => color.R > 190 && color.G > 180 && color.B < 50;
    private static bool IsDark(Color color) => color.R < 180 && color.G < 180 && color.B < 180 && !IsYellow(color);

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rows = ["A", "B"];
        string[,] assignments =
        {
            { "TER-001", "0A" },
            { "STN-003", "GLS-004" }
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
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000029"),
            DocumentRuntimeSerialNumber = 29,
            DocumentPath = "C:/design/extrusion-visuals.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "CW01",
            GeometryFingerprint = "extrusion-visuals-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [90d],
            VerticalOffsets = [45d],
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
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
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

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260813_TEST_panel-cladding-extrusion-visuals", fileName));

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
