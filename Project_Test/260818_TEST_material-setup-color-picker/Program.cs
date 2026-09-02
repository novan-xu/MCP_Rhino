using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.UI;

namespace MaterialSetupColorPickerSmoke;

internal static class Program
{
    private static readonly Color AcceptedColor = Color.FromRgb(0x12, 0xA4, 0xE8);

    [STAThread]
    private static void Main()
    {
        var acceptingPicker = new StubColorPicker(accepted: true, AcceptedColor);
        var acceptedDialog = CreateDialog(acceptingPicker);
        Button acceptedButton = Named<Button>(acceptedDialog, "CustomColorPreview");
        TextBox acceptedHex = Named<TextBox>(acceptedDialog, "HexColorText");
        ListBox acceptedPalette = Named<ListBox>(acceptedDialog, "PaletteList");

        VerifyButtonContract(acceptedButton);
        Require(acceptedPalette.SelectedItem is not null,
            "The acceptance fixture did not start with a suggested palette color selected.");
        Color initialPreview = BackgroundColor(acceptedButton);

        Click(acceptedButton);

        Require(acceptingPicker.CallCount == 1,
            "Clicking the custom-color preview did not invoke the picker exactly once.");
        Require(acceptingPicker.InitialColor == initialPreview,
            "The picker did not start from the color displayed by the preview.");
        Require(BackgroundColor(acceptedButton) == AcceptedColor,
            "The confirmed picker color did not update the custom-color preview.");
        Require(acceptedHex.Text == "#12A4E8",
            $"The confirmed picker color did not normalize the hex field: {acceptedHex.Text}");
        Require(acceptedPalette.SelectedItem is null,
            "A confirmed custom color left a suggested palette swatch selected.");
        Require(SelectedColor(acceptedDialog) == AcceptedColor,
            "The confirmed picker color did not update the effective material color.");

        Render(acceptedDialog, OutputPath("material-setup-color-picker-accepted.png"));

        var cancellingPicker = new StubColorPicker(accepted: false, AcceptedColor);
        var cancelledDialog = CreateDialog(cancellingPicker);
        Button cancelledButton = Named<Button>(cancelledDialog, "CustomColorPreview");
        TextBox cancelledHex = Named<TextBox>(cancelledDialog, "HexColorText");
        ListBox cancelledPalette = Named<ListBox>(cancelledDialog, "PaletteList");
        Color colorBeforeCancel = BackgroundColor(cancelledButton);
        string hexBeforeCancel = cancelledHex.Text;
        object? paletteBeforeCancel = cancelledPalette.SelectedItem;
        Color selectedBeforeCancel = SelectedColor(cancelledDialog);

        Click(cancelledButton);

        Require(cancellingPicker.CallCount == 1,
            "Clicking the cancellation fixture did not invoke the picker exactly once.");
        Require(cancellingPicker.InitialColor == colorBeforeCancel,
            "The cancelled picker did not receive the displayed initial color.");
        Require(BackgroundColor(cancelledButton) == colorBeforeCancel,
            "Cancelling the picker changed the preview color.");
        Require(cancelledHex.Text == hexBeforeCancel,
            "Cancelling the picker changed the hex field.");
        Require(ReferenceEquals(cancelledPalette.SelectedItem, paletteBeforeCancel),
            "Cancelling the picker changed the suggested-palette selection.");
        Require(SelectedColor(cancelledDialog) == selectedBeforeCancel,
            "Cancelling the picker changed the effective material color.");

        Console.WriteLine("[OK] The custom-color preview is an accessible mouse/keyboard button.");
        Console.WriteLine("[OK] Confirmed picker colors synchronize preview, hex, and effective color state.");
        Console.WriteLine("[OK] Cancelling the picker preserves all color and palette state.");
        Console.WriteLine("[OK] Compact hover, press, and keyboard-focus visual states are present.");
    }

    private static MaterialSetupDialog CreateDialog(IMaterialColorPicker picker) => new(
        controller: null,
        workbookPath: string.Empty,
        materials:
        [
            new PanelCladdingMaterial
            {
                Code = "MPL-001",
                Name = "Coil-coated metal silver smith",
                Category = "Metal",
                Color = Color.FromRgb(0x5E, 0x62, 0x65)
            }
        ],
        colorPicker: picker)
    {
        Width = 720,
        Height = 740,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false
    };

    private static void VerifyButtonContract(Button button)
    {
        Require(button.Focusable && button.IsTabStop,
            "The custom-color preview is not reachable through keyboard navigation.");
        Require(button.Cursor == Cursors.Hand,
            "The custom-color preview does not present a clickable pointer cursor.");
        Require(AutomationProperties.GetName(button) == "Choose custom material color",
            "The custom-color button is missing its accessible name.");
        Require(Math.Abs(button.Width - 40d) < 0.01d && Math.Abs(button.Height - 40d) < 0.01d,
            "The custom-color button no longer matches the compact 40-DIP swatch layout.");

        ControlTemplate template = button.Template;
        Require(HasTrigger(template, UIElement.IsMouseOverProperty),
            "The custom-color button has no hover feedback.");
        Require(HasTrigger(template, Button.IsPressedProperty),
            "The custom-color button has no pressed feedback.");
        Require(HasTrigger(template, UIElement.IsKeyboardFocusedProperty),
            "The custom-color button has no keyboard-focus feedback.");

        button.ApplyTemplate();
        Border swatchBorder = Descendants<Border>(button).First();
        Require(Math.Abs(swatchBorder.CornerRadius.TopLeft - 6d) < 0.01d,
            "The interactive preview lost the rounded swatch shape.");
    }

    private static bool HasTrigger(ControlTemplate template, DependencyProperty property) =>
        template.Triggers.OfType<Trigger>().Any(trigger =>
            trigger.Property == property && Equals(trigger.Value, true));

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    private static Color BackgroundColor(Control control) =>
        control.Background is SolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException("The custom-color preview has no solid background.");

    private static Color SelectedColor(MaterialSetupDialog dialog) =>
        (Color)(typeof(MaterialSetupDialog)
            .GetField("_selectedColor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(dialog) ?? throw new InvalidOperationException("Missing selected-color state."));

    private static T Named<T>(FrameworkElement owner, string name) where T : FrameworkElement =>
        owner.FindName(name) as T ?? throw new InvalidOperationException($"Missing named element {name}.");

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

    private static void Render(MaterialSetupDialog dialog, string path)
    {
        FrameworkElement root = (FrameworkElement)dialog.Content;
        root.Measure(new Size(720, 740));
        root.Arrange(new Rect(0, 0, 720, 740));
        root.UpdateLayout();

        var bitmap = new RenderTargetBitmap(720, 740, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        Require(new FileInfo(path).Length > 10_000, $"Render is unexpectedly empty: {path}");
    }

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(
        "Project_Test", "260818_TEST_material-setup-color-picker", fileName));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubColorPicker(bool accepted, Color result) : IMaterialColorPicker
    {
        public int CallCount { get; private set; }
        public Color InitialColor { get; private set; }

        public bool TryPick(Window owner, Color initialColor, out Color selectedColor)
        {
            Require(owner is MaterialSetupDialog,
                "The picker owner was not the Material Setup dialog.");
            CallCount++;
            InitialColor = initialColor;
            selectedColor = result;
            return accepted;
        }
    }
}
