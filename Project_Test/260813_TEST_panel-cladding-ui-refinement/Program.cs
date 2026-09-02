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

namespace PanelCladdingUiRefinementSmoke;

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
        var controller = new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, new StubWorkbookRepository(), renderer, signature),
            renderer,
            signature);
        var window = new PanelCladdingEditorWindow(controller)
        {
            Width = 1440d,
            Height = 900d,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        Require(window.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Fixture did not load.");
        FrameworkElement root = (FrameworkElement)window.Content;
        Layout(root, 1440, 900);

        Require(window.FindName("HeaderStatusText") is null, "The duplicate in-window product header remains.");
        Require(window.FindName("SelectionReadout") is null, "The toolbar selection sentence remains.");
        Require(Named<TextBlock>(window, "WallTypeText").Text == "CW-01", "Wall type was not formatted as CW-01.");
        Require(Named<TextBlock>(window, "UnitsText").Text == "in", "Panel units are incorrect.");
        PanelOffsetDisplay[] offsets = ((IEnumerable)Named<ItemsControl>(window, "DividerOffsetList").ItemsSource)
            .Cast<PanelOffsetDisplay>().ToArray();
        Require(offsets.Select(item => item.Display).SequenceEqual(
            ["29.90000", "76.40000", "49.20000", "82.70000", "142.50000"]),
            "Divider offsets are not displayed unit-free at exactly five decimal places.");

        Button cladding = Named<Button>(window, "CladdingViewButton");
        Button extrusion = Named<Button>(window, "ExtrusionViewButton");
        Require(Equals(cladding.Tag, "Selected") && ButtonFill(cladding) == Color.FromRgb(0x3E, 0x49, 0x4F),
            "Cladding view does not use the dark selected state.");
        extrusion.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Equals(extrusion.Tag, "Selected") && !Equals(cladding.Tag, "Selected"), "View selection highlight did not follow the active view.");
        cladding.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Button materialTab = Named<Button>(window, "MaterialTabButton");
        Button parentTab = Named<Button>(window, "ParentTabButton");
        Require(Equals(materialTab.Tag, "Selected") && ButtonFill(materialTab) == Colors.White,
            "Material mode is not highlighted white.");
        parentTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Equals(parentTab.Tag, "Selected") && ButtonFill(parentTab) == Colors.White,
            "Parent-cell mode is not highlighted white.");
        materialTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        ComboBox material = Named<ComboBox>(window, "MaterialSelect");
        ComboBox parent = Named<ComboBox>(window, "ParentReferenceSelect");
        material.ApplyTemplate();
        parent.ApplyTemplate();
        Require(material.FontFamily.Source == "Segoe UI" && parent.FontFamily.Source == "Segoe UI",
            "Assignment fields do not share the project typeface.");
        Require(Descendants<Border>(material).Any(border => border.CornerRadius.TopLeft >= 6d)
                && Descendants<Border>(parent).Any(border => border.CornerRadius.TopLeft >= 6d),
            "Assignment fields are not using rounded project templates.");

        PanelCladdingGridCanvas canvas = Named<PanelCladdingGridCanvas>(window, "PanelGridCanvas");
        Render(root, 1440, 900, OutputPath("panel-cladding-refined-base-1440x900.png"));
        canvas.SetSelection(Array.Empty<string>());
        RenderTargetBitmap baseCanvas = DrawCanvas(canvas);
        Dictionary<string, Rect> cellRects = Field<Dictionary<string, Rect>>(canvas, "_cellRects");
        string selectedKey = layout.Cells[1].UserTextKey;
        Rect selectedRect = cellRects[selectedKey];
        Color before = Pixel(baseCanvas, (int)(selectedRect.Left + 10d), (int)(selectedRect.Top + 10d));
        canvas.SetSelection([selectedKey]);
        RenderTargetBitmap selectedCanvas = DrawCanvas(canvas);
        Color after = Pixel(selectedCanvas, (int)(selectedRect.Left + 10d), (int)(selectedRect.Top + 10d));
        Require(after.R == 0xFF && after.G == 0xEA && after.B == 0x00 && after.A == 0xFF,
            $"Selected cladding cell is not an opaque bright-yellow overwrite (before {before}, after {after}).");
        Render(root, 1440, 900, OutputPath("panel-cladding-refined-selection-1440x900.png"));

        Invoke(canvas, "BeginInlineDimensionEdit", PanelDimensionAxis.Column, 0, 49.2d, new Rect(280d, 22d, 86d, 30d));
        Require(canvas.IsDimensionEditorOpen, "Dimension click path did not create an inline editor.");
        TextBox inline = Descendants<TextBox>(canvas).Single(textBox => textBox.FontFamily.Source == "Consolas");
        Require(inline.Text == "49.20000", "Inline dimension editor is not initialized at five decimals.");
        inline.Text = "48.75000";
        Render(root, 1440, 900, OutputPath("panel-cladding-refined-inline-edit-1440x900.png"));
        Invoke(canvas, "CommitInlineDimensionEdit");
        Require(!canvas.IsDimensionEditorOpen, "Inline dimension editor did not close after commit.");
        Require(Math.Abs(Field<List<double>>(window, "_verticalOffsets")[0] - 48.75d) < 1e-8d,
            "Inline dimension commit did not update the layout.");

        string canvasSource = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(canvasSource.Contains("new Point(cursor, y + 10d)", StringComparison.Ordinal)
                && canvasSource.Contains("new Point(x + 10d, cursor)", StringComparison.Ordinal),
            "Dimension witness marks do not point toward the panel.");
        Require(!typeof(PanelCladdingEditorWindow).Assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, "Eto", StringComparison.OrdinalIgnoreCase)
                                  || reference.Name?.Contains("WebView", StringComparison.OrdinalIgnoreCase) == true),
            "Editor must remain a native WPF window without Eto or WebView.");

        Console.WriteLine("[OK] Metadata hierarchy and five-decimal divider offsets passed.");
        Console.WriteLine("[OK] Bright-yellow selection overwrite and panel-facing dimension ticks passed.");
        Console.WriteLine("[OK] Inline dimension editing committed without a popup window.");
        Console.WriteLine("[OK] Active modes, typography, rounded fields, and redundant-UI removal passed.");
    }

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        method.Invoke(instance, arguments);
    }

    private static Color ButtonFill(Button button)
    {
        button.ApplyTemplate();
        return Descendants<Border>(button).First().Background is SolidColorBrush solid
            ? solid.Color
            : throw new InvalidOperationException("Expected a solid button fill.");
    }

    private static void Layout(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0d, 0d, width, height));
        root.UpdateLayout();
    }

    private static RenderTargetBitmap Snapshot(FrameworkElement root)
    {
        int width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }

    private static RenderTargetBitmap DrawCanvas(PanelCladdingGridCanvas canvas)
    {
        int width = Math.Max(1, (int)Math.Ceiling(canvas.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(canvas.ActualHeight));
        var visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
        {
            Invoke(canvas, "OnRender", context);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void Render(FrameworkElement root, int width, int height, string path)
    {
        Layout(root, width, height);
        RenderTargetBitmap bitmap = Snapshot(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 20_000L, $"Render is unexpectedly empty: {path}");
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

    private static string OutputPath(string name) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260813_TEST_panel-cladding-ui-refinement", name));

    private static PanelCladdingLayout BuildLayout()
    {
        string[] rows = ["A", "B", "C"];
        string[] codes = ["TER-01", "GLS-004", "STN-003", "TER-01", "GLS-005", "STN-003"];
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
                    Value = codes[row * 2 + column]
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000001"),
            DocumentRuntimeSerialNumber = 1,
            DocumentPath = "C:/design/panel-cladding.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "CW01",
            GeometryFingerprint = "ui-refinement-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            GeometryDiagnostic = "Planar Brep is projectable.",
            Width = 181.1d,
            Height = 180d,
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
            OperationResponse<PanelCladdingMatchPanelSnapshot>.Fail("Not used.");
        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(
            PanelAttributeCommitRequest request, Func<OperationResponse> finalizeExternalCommit) =>
            OperationResponse<PanelAttributeCommitResult>.Fail("Not used.");
        public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
            OperationResponse<string>.Fail("Not used.");
    }

    private sealed class StubWorkbookRepository : IPanelCladdingWorkbookRepository
    {
        public OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookUpdate>.Fail("Not used.");
        public OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
            PanelCladdingWorkbookBatchUpsert request) =>
            OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate>.Fail("Not used.");
    }
}
