using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.PanelCladding;
using PanelCladdingEditor.UI;

namespace MaterialCatalogueEditingSmoke;

internal static class Program
{
    private static readonly Color OriginalColor = Color.FromRgb(0x21, 0x35, 0x47);
    private static readonly Color EditedColor = Color.FromRgb(0xD2, 0x69, 0x1E);
    private static string _outputDirectory = Path.Combine("Project_Test", "260818_TEST_material-catalogue-editing");

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            _outputDirectory = Path.GetFullPath(args[0]);
        }
        Directory.CreateDirectory(_outputDirectory);
        var colorPicker = new StubColorPicker(EditedColor);
        var categoryPrompt = new StubCategoryPrompt("Wood", "wood");
        PanelCladdingMaterial[] fixture =
        [
            Material("MET-001", "Silver smith coil coating", "Metal", OriginalColor),
            Material("GLS-003", "Clear vision glazing", "Glass", Color.FromRgb(0x71, 0xA0, 0xAE))
        ];
        var dialog = new MaterialSetupDialog(null, string.Empty, fixture, colorPicker, categoryPrompt)
        {
            Width = 720,
            Height = 740,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };

        FrameworkElement root = (FrameworkElement)dialog.Content;
        Layout(root, 720, 740);

        Border workbook = Named<Border>(dialog, "WorkbookSection");
        Border catalogue = Named<Border>(dialog, "CatalogueSection");
        Border definition = Named<Border>(dialog, "DefinitionSection");
        Require(Top(workbook, root) < Top(catalogue, root) && Top(catalogue, root) < Top(definition, root),
            "Material Setup sections are not ordered workbook, catalogue, definition.");

        ListBox configured = Named<ListBox>(dialog, "ConfiguredList");
        WrapPanel cataloguePanel = Descendants<WrapPanel>(configured).Single();
        Require(!Descendants<UniformGrid>(configured).Any(),
            "The material catalogue still uses a fixed-column uniform grid.");
        Require(configured.MaxHeight >= 220d && dialog.CatalogueItemWidth >= 76d,
            "The material catalogue is not a taller, code-width-governed wrapping viewport.");
        ListBoxItem firstTile = (ListBoxItem)(configured.ItemContainerGenerator.ContainerFromIndex(0)
            ?? throw new InvalidOperationException("The first catalogue tile was not generated."));
        Require(Math.Abs(firstTile.ActualWidth - dialog.CatalogueItemWidth) < 0.01d &&
                cataloguePanel.ActualWidth > firstTile.ActualWidth,
            "The catalogue tile width is not shared with a wider responsive wrapping panel.");
        TextBlock[] tileText = Descendants<TextBlock>(firstTile).ToArray();
        Require(tileText.Length == 1 && tileText[0].Text == "MET-001",
            "The catalogue tile preview contains fields other than the material code.");
        Require(Descendants<Border>(firstTile).Any(border =>
                border.Background is SolidColorBrush brush && brush.Color == OriginalColor),
            "The catalogue tile does not include its material color swatch.");

        ComboBox category = Named<ComboBox>(dialog, "CategorySelect");
        Button addCategory = Named<Button>(dialog, "AddCategoryButton");
        TextBox code = Named<TextBox>(dialog, "CodeText");
        TextBox description = Named<TextBox>(dialog, "NameText");
        ListBox palette = Named<ListBox>(dialog, "PaletteList");
        TextBlock colorAssist = Named<TextBlock>(dialog, "ColorAssistText");
        TextBox hex = Named<TextBox>(dialog, "HexColorText");
        Button preview = Named<Button>(dialog, "CustomColorPreview");
        Button action = Named<Button>(dialog, "MaterialActionButton");

        configured.SelectedIndex = 0;
        Require(code.Text == "MET-001", "Catalogue selection did not load the material code.");
        Require((string?)category.SelectedItem == "Metal", "Catalogue selection did not load the category.");
        Require(description.Text == "Silver smith coil coating",
            "Catalogue selection did not load the material description.");
        Require(hex.Text == "#213547" && BackgroundColor(preview) == OriginalColor,
            "Catalogue selection did not preserve the material's exact custom color.");
        Require(palette.SelectedItem is null,
            "Loading a custom material color incorrectly selected a category suggestion.");
        Require((string?)action.Content == "Save edits",
            "An existing material code did not change the action to Save edits.");

        Click(addCategory);
        Require(categoryPrompt.CallCount == 1 &&
                category.Items.Cast<string>().Count(item => item == "Wood") == 1 &&
                (string?)category.SelectedItem == "Wood",
            "Add category did not add and select the requested custom category exactly once.");
        Require(palette.Items.Count == 5,
            "A custom category did not receive the five-color fallback suggestion palette.");

        description.Text = "Exterior-grade thermally modified wood";
        Click(preview);
        Require(colorPicker.InitialColor != EditedColor && BackgroundColor(preview) == EditedColor,
            "The custom color picker did not update the edit fixture color.");
        int countBeforeEdit = dialog.Materials.Count;
        Click(action);
        Require(dialog.Materials.Count == countBeforeEdit,
            "Saving an existing material changed the catalogue count.");
        PanelCladdingMaterial edited = dialog.Materials.Single(item => item.Code == "MET-001");
        Require(edited.Name == "Exterior-grade thermally modified wood" &&
                edited.Category == "Wood" && edited.Color == EditedColor,
            "Save edits did not replace the matched material with all edited attributes.");
        Require(ReferenceEquals(configured.SelectedItem, edited) && (string?)action.Content == "Save edits",
            "The saved catalogue tile was not retained as the active edit target.");

        Click(addCategory);
        Require(categoryPrompt.CallCount == 2 &&
                category.Items.Cast<string>().Count(item =>
                    string.Equals(item, "Wood", StringComparison.OrdinalIgnoreCase)) == 1,
            "Category addition allowed a case-insensitive duplicate.");

        code.Text = "NEW-001";
        description.Text = "New composite finish";
        category.SelectedItem = "Composite";
        Require(configured.SelectedItem is null && (string?)action.Content == "Add material" &&
                !Named<Button>(dialog, "RemoveMaterialButton").IsEnabled,
            "An unused code did not clear the stale edit/delete target and show Add material.");
        int countBeforeAdd = dialog.Materials.Count;
        Click(action);
        Require(dialog.Materials.Count == countBeforeAdd + 1 &&
                dialog.Materials.Count(item => item.Code == "NEW-001") == 1,
            "Add material did not append exactly one unused code.");
        Require((string?)action.Content == "Save edits" &&
                configured.SelectedItem is PanelCladdingMaterial { Code: "NEW-001" },
            "A newly added material did not become the active Save edits target.");

        var reopened = new MaterialSetupDialog(
            null,
            string.Empty,
            dialog.Materials,
            new StubColorPicker(EditedColor),
            new StubCategoryPrompt("Unused"));
        Require(Named<ComboBox>(reopened, "CategorySelect").Items.Cast<string>().Contains("Wood"),
            "A custom category stored by a material was not restored in a new setup session.");

        VerifyLayout(dialog, root, category, description, palette, colorAssist);
        Render(root, 720, 740, OutputPath("material-catalogue-editing-720x740.png"));
        Render(root, 620, 640, OutputPath("material-catalogue-editing-620x640.png"));
        VerifyRemoval();

        Console.WriteLine("[OK] Catalogue selection loads exact material attributes without color replacement.");
        Console.WriteLine("[OK] Existing codes save in place; unused codes add exactly one material.");
        Console.WriteLine("[OK] Category dropdown/addition and case-insensitive duplicate protection passed.");
        Console.WriteLine("[OK] Responsive catalogue wrap, expanded description, palette bounds, and equal footer passed.");
    }

    private static void VerifyRemoval()
    {
        PanelCladdingMaterial[] source =
        [
            .. Enumerable.Range(1, 14).Select(index =>
                Material($"GLS-{index:000}", $"Glass finish {index}", "Glass", OriginalColor)),
            Material("LONG-MATERIAL-01", "Long-code finish", "Metal", EditedColor)
        ];
        var dialog = new MaterialSetupDialog(null, string.Empty, source,
            new StubColorPicker(EditedColor), new StubCategoryPrompt("Unused"));
        FrameworkElement root = (FrameworkElement)dialog.Content;
        ListBox configured = Named<ListBox>(dialog, "ConfiguredList");
        Button remove = Named<Button>(dialog, "RemoveMaterialButton");
        Require(!remove.IsEnabled, "Removal must be disabled with no selected material.");
        Click(remove);
        Require(dialog.Materials.Count == source.Length, "No-selection removal changed the catalogue.");
        configured.SelectedIndex = 0;
        Require(remove.IsEnabled, "Selecting a catalogue material did not enable removal.");
        foreach ((int width, int height) in new[] { (720, 740), (620, 640) })
        {
            Layout(root, width, height);
            Require(Left(remove, root) >= Left(configured, root) + configured.ActualWidth &&
                    Left(remove, root) + remove.ActualWidth <= width &&
                    remove.ActualWidth == 40d && remove.ActualHeight == 40d,
                "Trash button overlaps the catalogue or is clipped at the supported dialog size.");
            Render(root, width, height, OutputPath($"material-catalogue-removal-{width}x{height}.png"));
        }

        string workbookPath = Path.Combine(Path.GetTempPath(), $"material-removal-{Guid.NewGuid():N}.xlsx");
        var repository = new OpenXmlPanelCladdingWorkbookRepository();
        void SaveCatalogue()
        {
            var prepared = repository.PrepareMaterialCatalog(new PanelCladdingMaterialCatalogSaveRequest
            {
                WorkbookPath = workbookPath,
                AllowCreate = !File.Exists(workbookPath),
                Materials = dialog.Materials.Select(item => item.ToCatalogItem()).ToArray()
            });
            Require(prepared.Success && prepared.Data is not null, prepared.Message);
            using var update = prepared.Data!;
            var committed = update.Commit();
            Require(committed.Success, committed.Message);
        }
        try
        {
            SaveCatalogue();
            byte[] originalWorkbook = File.ReadAllBytes(workbookPath);
            configured.SelectedItem = dialog.Materials.Last();
            double priorWidth = dialog.CatalogueItemWidth;
            Click(remove);
            Require(dialog.Materials.Count == source.Length - 1 &&
                    dialog.Materials.Select(item => item.Code).SequenceEqual(source.SkipLast(1).Select(item => item.Code)),
                "Removal did not preserve every unselected material in order.");
            Require(configured.SelectedItem is null && !remove.IsEnabled &&
                    Named<TextBox>(dialog, "CodeText").Text == string.Empty &&
                    Named<TextBox>(dialog, "NameText").Text == string.Empty &&
                    (string?)Named<Button>(dialog, "MaterialActionButton").Content == "Add material",
                "Removal retained a stale deletion target or material edit fields.");
            Require(dialog.CatalogueItemWidth < priorWidth, "Removal did not recalculate tile width.");
            Click(remove);
            Require(dialog.Materials.Count == source.Length - 1, "Repeated removal deleted an unselected entry.");
            Require(source.Length == 15 && source.Last().Code == "LONG-MATERIAL-01" &&
                    File.ReadAllBytes(workbookPath).SequenceEqual(originalWorkbook),
                "Pending removal mutated the caller catalogue or workbook before confirmation.");
            var reopened = new MaterialSetupDialog(source);
            Require(reopened.Materials.Count == source.Length,
                "Discarding the working copy lost a source material.");

            SaveCatalogue();
            var reduced = repository.ReadMaterialCatalog(workbookPath);
            Require(reduced.Success && reduced.Data is not null &&
                    reduced.Data.Materials.Select(item => item.Code).SequenceEqual(dialog.Materials.Select(item => item.Code)),
                "The reduced catalogue did not persist through the existing save path.");
            while (dialog.Materials.Count > 0)
            {
                configured.SelectedIndex = 0;
                Click(remove);
            }
            Require(!remove.IsEnabled, "Removing the last material left removal enabled.");
            SaveCatalogue();
            var empty = repository.ReadMaterialCatalog(workbookPath);
            Require(empty.Success && empty.Data is { Materials.Count: 0, UsesLegacyTypeFallback: false },
                "An empty catalogue did not persist as an explicit empty Materials sheet.");
        }
        finally
        {
            File.Delete(workbookPath);
        }
        Console.WriteLine("[OK] Selected-only removal, no-selection guard, edit reset, and copy isolation passed.");
        Console.WriteLine("[OK] Reduced and empty catalogue persistence and trash-button layout passed.");
    }

    private static void VerifyLayout(
        MaterialSetupDialog dialog,
        FrameworkElement root,
        ComboBox category,
        TextBox description,
        ListBox palette,
        TextBlock colorAssist)
    {
        Require(category.Height == 40d && Named<Button>(dialog, "AddCategoryButton").Height == 40d,
            "Category dropdown and Add category button are not aligned 40-DIP controls.");
        Require(description.AcceptsReturn && description.TextWrapping == TextWrapping.Wrap &&
                Math.Abs(description.Height - 128d) < 0.01d,
            "The material description is not the requested expanded multiline editor.");
        double descriptionBottom = Bottom(description, root);
        double assistBottom = Bottom(colorAssist, root);
        Require(Math.Abs(descriptionBottom - assistBottom) <= 2d,
            $"Description bottom does not align with suggested-color help ({descriptionBottom:0.##}/{assistBottom:0.##}).");

        Require(Math.Abs(palette.Height - 36d) < 0.01d,
            "The suggested palette viewport was not enlarged beyond its 32-DIP items.");
        foreach (object item in palette.Items)
        {
            ListBoxItem container = (ListBoxItem)(palette.ItemContainerGenerator.ContainerFromItem(item)
                ?? throw new InvalidOperationException("A palette swatch container was not generated."));
            double top = Top(container, palette);
            double bottom = Bottom(container, palette);
            Require(top >= -0.01d && bottom <= palette.ActualHeight + 0.01d,
                $"Suggested swatch is clipped by the palette viewport ({top:0.##}-{bottom:0.##}/{palette.ActualHeight:0.##}).");
        }

        Button cancel = Named<Button>(dialog, "CancelButton");
        Button action = Named<Button>(dialog, "MaterialActionButton");
        Button confirm = Named<Button>(dialog, "ConfirmButton");
        Require(Math.Abs(cancel.ActualWidth - action.ActualWidth) < 0.01d &&
                Math.Abs(action.ActualWidth - confirm.ActualWidth) < 0.01d,
            "Cancel, material action, and Confirm are not equal-width footer buttons.");
        Require(Left(cancel, root) < Left(action, root) && Left(action, root) < Left(confirm, root) &&
                (string?)confirm.Content == "Confirm",
            "Footer action order or Confirm label is incorrect.");
        Require(action.Background is SolidColorBrush { Color: var editColor } &&
                editColor == Color.FromRgb(0xD9, 0x77, 0x06),
            "The Add material / Save edits footer action is not orange.");
    }

    private static PanelCladdingMaterial Material(string code, string name, string category, Color color) => new()
    {
        Code = code,
        Name = name,
        Category = category,
        Color = color
    };

    private static void Click(Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    private static Color BackgroundColor(Control control) =>
        control.Background is SolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException("Expected a solid color background.");

    private static double Left(FrameworkElement element, UIElement relativeTo) =>
        element.TranslatePoint(new Point(0, 0), relativeTo).X;

    private static double Top(FrameworkElement element, UIElement relativeTo) =>
        element.TranslatePoint(new Point(0, 0), relativeTo).Y;

    private static double Bottom(FrameworkElement element, UIElement relativeTo) =>
        element.TranslatePoint(new Point(0, element.ActualHeight), relativeTo).Y;

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

    private static string OutputPath(string fileName) => Path.GetFullPath(Path.Combine(_outputDirectory, fileName));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class StubColorPicker(Color result) : IMaterialColorPicker
    {
        public Color InitialColor { get; private set; }

        public bool TryPick(Window owner, Color initialColor, out Color selectedColor)
        {
            InitialColor = initialColor;
            selectedColor = result;
            return true;
        }
    }

    private sealed class StubCategoryPrompt(params string[] results) : IMaterialCategoryPrompt
    {
        private readonly Queue<string> _results = new(results);

        public int CallCount { get; private set; }

        public bool TryGetCategory(
            Window owner,
            IReadOnlyCollection<string> existingCategories,
            out string category)
        {
            CallCount++;
            category = _results.Dequeue();
            return true;
        }
    }
}
