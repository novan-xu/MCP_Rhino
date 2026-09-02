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

namespace PanelCladdingVisualPolishSmoke;

internal static class Program
{
    private static readonly Color Terracotta = Color.FromRgb(200, 114, 71);

    [STAThread]
    private static void Main()
    {
        PanelCladdingLayout layout = BuildLayout();
        PanelCladdingMaterial material = new()
        {
            Code = "TER-001",
            Name = "Terracotta finish",
            Category = "Terracotta",
            Color = Terracotta
        };
        var values = layout.Cells.ToDictionary(cell => cell.UserTextKey, cell => cell.Value);
        var materials = new Dictionary<string, PanelCladdingMaterial>(StringComparer.OrdinalIgnoreCase)
        {
            [material.Code] = material
        };

        var canvas = new PanelCladdingGridCanvas { Width = 660, Height = 540 };
        canvas.SetLayout(layout, values, materials, []);
        Layout(canvas, 660, 540);
        Render(canvas, 660, 540, OutputPath("parent-boundary-660x540.png"));
        VerifyBoundaryPixels(canvas);

        string canvasSource = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "PanelCladdingEditor", "UI", "PanelCladdingGridCanvas.cs"));
        Require(!canvasSource.Contains("Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)", StringComparison.Ordinal),
            "Right-button pan still requires Shift.");
        Require(canvasSource.Contains("var dashPen = new Pen(GridLine, 1.5d)", StringComparison.Ordinal) &&
                canvasSource.Contains("var gapPen = new Pen(gapBrush, 1.5d)", StringComparison.Ordinal),
            "Parent dash and gap strokes do not match the 1.5-DIP solid divider weight.");
        Require(!canvasSource.Contains("new Pen(Surface, 3d)", StringComparison.Ordinal),
            "The white parent-boundary clearing halo remains.");

        var rightDown = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
        {
            RoutedEvent = UIElement.MouseRightButtonDownEvent
        };
        Invoke(canvas, "OnMouseRightButtonDown", canvas, rightDown);
        Require(Field<bool>(canvas, "_isPanning"), "An unmodified right-button press did not begin pan.");
        var rightUp = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
        {
            RoutedEvent = UIElement.MouseRightButtonUpEvent
        };
        Invoke(canvas, "OnMouseRightButtonUp", canvas, rightUp);
        Require(!Field<bool>(canvas, "_isPanning"), "Right-button release did not end pan.");

        var repository = new CapturingRepository(layout);
        var keys = new PanelCladdingKeyService();
        var renderer = new PanelPreviewRenderer();
        var signature = new PanelCladdingTypeSignatureService(keys);
        var controller = new PanelCladdingEditorController(
            repository,
            new PanelCladdingSaveService(repository, signature),
            renderer,
            signature);
        var editor = new PanelCladdingEditorWindow(controller)
        {
            Width = 1380,
            Height = 860,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        Require(editor.LoadPanel(layout.DocumentPath, layout.ObjectId).Success, "Editor fixture failed to load.");
        FrameworkElement editorRoot = (FrameworkElement)editor.Content;
        Layout(editorRoot, 1380, 860);
        TextBox offsetEditor = Descendants<TextBox>(Named<ItemsControl>(editor, "DividerOffsetList")).First();
        VerifyOffsetEditorStyle(offsetEditor);

        var setup = new MaterialSetupDialog([material])
        {
            Width = 720,
            Height = 740,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };
        FrameworkElement setupRoot = (FrameworkElement)setup.Content;
        Layout(setupRoot, 720, 740);
        VerifySetupDialog(setup, setupRoot);
        Render(setupRoot, 720, 740, OutputPath("material-setup-720x740.png"));

        Console.WriteLine("[OK] Right-button pan works without Shift and retains release handling.");
        Console.WriteLine("[OK] Parent dashes use material-backed gaps and the solid divider weight without a white halo.");
        Console.WriteLine("[OK] Divider offsets expose a rounded temporary green edit outline.");
        Console.WriteLine("[OK] Material Setup matches the main editor's typography, sizing, spacing, and rounded controls.");
    }

    private static void VerifyBoundaryPixels(PanelCladdingGridCanvas canvas)
    {
        Dictionary<string, Rect> rects = Field<Dictionary<string, Rect>>(canvas, "_cellRects");
        Rect parent = rects[PanelCladdingKeyService.GetCellKey(0, "A")];
        Rect child = rects[PanelCladdingKeyService.GetCellKey(1, "A")];
        int x = (int)Math.Round((parent.Right + child.Left) / 2d);
        var bitmap = new RenderTargetBitmap(660, 540, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        int materialPixels = 0;
        int darkPixels = 0;
        int whitePixels = 0;
        int top = (int)Math.Ceiling(child.Top + 18d);
        int bottom = (int)Math.Floor(child.Bottom - 18d);
        for (int y = top; y <= bottom; y++)
        {
            if (Math.Abs(y - (child.Top + child.Height / 2d)) < 22d)
            {
                continue;
            }
            Color color = Pixel(pixels, stride, x, y);
            if (Near(color, Terracotta, 10) ||
                (color.R > 120 && color.G >= 80 && color.G <= 125 && color.B >= 55 && color.B <= 90))
            {
                materialPixels++;
            }
            if (color.R < 90 && color.G < 100 && color.B < 105)
            {
                darkPixels++;
            }
            if (color.R > 245 && color.G > 245 && color.B > 245)
            {
                whitePixels++;
            }
        }
        Require(materialPixels > 12 && darkPixels > 12,
            $"Boundary did not contain both material gaps and dark dashes ({materialPixels}/{darkPixels}).");
        Require(whitePixels == 0, $"Boundary still contains {whitePixels} white-halo pixels.");
    }

    private static void VerifyOffsetEditorStyle(TextBox textBox)
    {
        Require(textBox.Style is not null && Math.Abs(textBox.Height - 25d) < 0.01d,
            "Divider offset editor does not use the compact inline edit style.");
        Require(textBox.BorderBrush is SolidColorBrush { Color.A: 0 } &&
                Math.Abs(textBox.BorderThickness.Left - 1.5d) < 0.01d,
            "Divider offset editor does not reserve a transparent, non-shifting focus outline.");
        ControlTemplate template = textBox.Template;
        Trigger? focusTrigger = template.Triggers.OfType<Trigger>().FirstOrDefault(trigger =>
            trigger.Property == UIElement.IsKeyboardFocusedProperty && Equals(trigger.Value, true));
        Require(focusTrigger is not null, "Divider offset editor has no temporary keyboard-focus state.");
        Setter? greenBorder = focusTrigger!.Setters.OfType<Setter>().FirstOrDefault(setter =>
            setter.Property == Border.BorderBrushProperty && setter.TargetName == "EditorBorder");
        Require(greenBorder?.Value is SolidColorBrush brush && brush.Color == Color.FromRgb(34, 148, 71),
            "Divider offset focus state is not the editor's green outline.");
        textBox.ApplyTemplate();
        Border roundedBorder = Descendants<Border>(textBox).First();
        Require(Math.Abs(roundedBorder.CornerRadius.TopLeft - 5d) < 0.01d,
            "Divider offset focus outline is not rounded like canvas dimension editing.");
    }

    private static void VerifySetupDialog(MaterialSetupDialog dialog, FrameworkElement root)
    {
        Require(dialog.FontFamily.Source == "Segoe UI" && Math.Abs(dialog.FontSize - 14d) < 0.01d,
            "Material Setup typography differs from the main editor.");
        foreach (string name in new[] { "WorkbookPathText", "CodeText", "HexColorText" })
        {
            TextBox field = Named<TextBox>(dialog, name);
            Require(Math.Abs(field.Height - 40d) < 0.01d && field.FontFamily.Source is "Segoe UI" or "Consolas",
                $"{name} does not use the aligned 40-DIP form field.");
            field.ApplyTemplate();
            Require(Descendants<Border>(field).Any(border => Math.Abs(border.CornerRadius.TopLeft - 6d) < 0.01d),
                $"{name} is not a rounded editor field.");
        }
        TextBox description = Named<TextBox>(dialog, "NameText");
        Require(Math.Abs(description.Height - 128d) < 0.01d && description.AcceptsReturn &&
                description.TextWrapping == TextWrapping.Wrap,
            "Material description does not use the expanded multiline editor.");
        description.ApplyTemplate();
        Require(Descendants<Border>(description).Any(border => Math.Abs(border.CornerRadius.TopLeft - 6d) < 0.01d),
            "Material description is not a rounded editor field.");
        Button browse = Named<Button>(dialog, "BrowseWorkbookButton");
        Require(Math.Abs(browse.Height - 40d) < 0.01d,
            "Workbook field and browse button heights are not aligned.");
        Require(Math.Abs(Named<ComboBox>(dialog, "CategorySelect").Height - 40d) < 0.01d &&
                Math.Abs(Named<ListBox>(dialog, "PaletteList").Height - 36d) < 0.01d,
            "Category dropdown or unclipped color selector has the wrong compact height.");
        Require(Named<ListBox>(dialog, "ConfiguredList") is not ListView,
            "Material catalogue must remain a custom tile grid rather than native GridView.");
        Require(root.ActualWidth >= 719d && root.ActualHeight >= 739d,
            "Material Setup root did not lay out at its target design size.");
    }

    private static PanelCladdingLayout BuildLayout()
    {
        PanelCladdingCell parent = new()
        {
            Column = 0,
            Row = 0,
            RowLabel = "A",
            ShortLabel = "0A",
            UserTextKey = PanelCladdingKeyService.GetCellKey(0, "A"),
            Value = "TER-001"
        };
        PanelCladdingCell child = new()
        {
            Column = 1,
            Row = 0,
            RowLabel = "A",
            ShortLabel = "1A",
            UserTextKey = PanelCladdingKeyService.GetCellKey(1, "A"),
            Value = "0A"
        };
        return new PanelCladdingLayout
        {
            ObjectId = Guid.Parse("A0427000-0000-0000-0000-000000000028"),
            DocumentRuntimeSerialNumber = 28,
            DocumentPath = "C:/design/visual-polish.3dm",
            ObjectName = "PID-A0427",
            LayerFullPath = "01_CW Panels::Surface",
            SystemCode = "CW01",
            GeometryFingerprint = "visual-polish-fixture",
            GeometryClass = PanelGeometryClass.Planar,
            Width = 90d,
            Height = 180d,
            ModelTolerance = 0.001d,
            ModelUnitScaleToMillimeters = 25.4d,
            HorizontalOffsets = [],
            VerticalOffsets = [45d],
            Cells = [parent, child]
        };
    }

    private static Color Pixel(byte[] pixels, int stride, int x, int y)
    {
        int offset = y * stride + x * 4;
        return Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static bool Near(Color first, Color second, int tolerance) =>
        Math.Abs(first.R - second.R) <= tolerance &&
        Math.Abs(first.G - second.G) <= tolerance &&
        Math.Abs(first.B - second.B) <= tolerance;

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(object instance, string name, params object[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);
        _ = method.Invoke(instance, arguments);
    }

    private static void Layout(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
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
        Require(new FileInfo(path).Length > 10_000, $"Render is unexpectedly empty: {path}");
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
        "Project_Test", "260813_TEST_panel-cladding-visual-polish", fileName));

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
