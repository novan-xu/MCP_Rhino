using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public partial class MaterialSetupDialog : Window, INotifyPropertyChanged
{
    private const double MinimumCatalogueItemWidth = 76d;
    private const double CatalogueItemChromeWidth = 36d;
    private readonly ObservableCollection<PanelCladdingMaterial> _materials;
    private readonly ObservableCollection<MaterialColorChoice> _palette = [];
    private readonly ObservableCollection<string> _categories = [];
    private readonly PanelCladdingEditorController? _controller;
    private readonly IMaterialColorPicker _colorPicker;
    private readonly IMaterialCategoryPrompt _categoryPrompt;
    private Color _selectedColor;
    private bool _isPopulatingEditor;
    private double _catalogueItemWidth = MinimumCatalogueItemWidth;

    public MaterialSetupDialog(IEnumerable<PanelCladdingMaterial> materials)
        : this(null, string.Empty, materials)
    {
    }

    public MaterialSetupDialog(
        PanelCladdingEditorController? controller,
        string workbookPath,
        IEnumerable<PanelCladdingMaterial> materials)
        : this(controller, workbookPath, materials, new RhinoMaterialColorPicker())
    {
    }

    public MaterialSetupDialog(
        PanelCladdingEditorController? controller,
        string workbookPath,
        IEnumerable<PanelCladdingMaterial> materials,
        IMaterialColorPicker colorPicker)
        : this(controller, workbookPath, materials, colorPicker, new MaterialCategoryPrompt())
    {
    }

    public MaterialSetupDialog(
        PanelCladdingEditorController? controller,
        string workbookPath,
        IEnumerable<PanelCladdingMaterial> materials,
        IMaterialColorPicker colorPicker,
        IMaterialCategoryPrompt categoryPrompt)
    {
        InitializeComponent();
        _controller = controller;
        _colorPicker = colorPicker ?? throw new ArgumentNullException(nameof(colorPicker));
        _categoryPrompt = categoryPrompt ?? throw new ArgumentNullException(nameof(categoryPrompt));
        _materials = new ObservableCollection<PanelCladdingMaterial>(materials.Select(item => item.Copy()));
        Materials = _materials;
        WorkbookPath = workbookPath;
        WorkbookPathText.Text = workbookPath;
        ConfiguredList.ItemsSource = _materials;
        UpdateCatalogueItemWidth();
        foreach (string category in PanelCladdingMaterialPalette.Categories)
        {
            EnsureCategory(category);
        }
        foreach (string category in _materials.Select(item => item.Category))
        {
            EnsureCategory(category);
        }
        CategorySelect.ItemsSource = _categories;
        PaletteList.ItemsSource = _palette;
        CategorySelect.SelectedIndex = 0;
        UpdateMaterialActionState();
        if (!string.IsNullOrWhiteSpace(workbookPath) && File.Exists(workbookPath))
        {
            LoadCatalog(workbookPath);
        }
    }

    public IReadOnlyList<PanelCladdingMaterial> Materials { get; }
    public string WorkbookPath { get; private set; } = string.Empty;
    public double CatalogueItemWidth
    {
        get => _catalogueItemWidth;
        private set
        {
            if (Math.Abs(_catalogueItemWidth - value) < 0.01d)
            {
                return;
            }
            _catalogueItemWidth = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnBrowseWorkbookClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose or create the project material workbook",
            FileName = string.IsNullOrWhiteSpace(WorkbookPathText.Text)
                ? "panel-cladding-materials.xlsx"
                : WorkbookPathText.Text.Trim(),
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }
        WorkbookPath = dialog.FileName;
        WorkbookPathText.Text = WorkbookPath;
        if (File.Exists(WorkbookPath))
        {
            LoadCatalog(WorkbookPath);
        }
        else
        {
            WorkbookStatusText.Text = "A new Materials catalogue will be created when you choose Confirm.";
        }
    }

    private void LoadCatalog(string workbookPath)
    {
        if (_controller is null)
        {
            return;
        }
        OperationResponse<PanelCladdingMaterialCatalog> response = _controller.LoadMaterialCatalog(workbookPath);
        if (!response.Success || response.Data is null)
        {
            WorkbookStatusText.Text = response.Message;
            return;
        }
        _materials.Clear();
        foreach (PanelCladdingMaterialCatalogItem item in response.Data.Materials)
        {
            try
            {
                PanelCladdingMaterial material = PanelCladdingMaterial.FromCatalogItem(item);
                _materials.Add(material);
                EnsureCategory(material.Category);
            }
            catch (Exception exception) when (exception is FormatException or NotSupportedException)
            {
                WorkbookStatusText.Text = $"Skipped {item.Code}: its color is not a valid #RRGGBB value.";
            }
        }
        UpdateCatalogueItemWidth();
        WorkbookStatusText.Text = response.Data.UsesLegacyTypeFallback
            ? $"Loaded {_materials.Count} materials from legacy cladding sheets; saving will migrate them to Materials."
            : $"Loaded {_materials.Count} project materials from the Materials tab.";
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategorySelect.SelectedItem is not string category)
        {
            return;
        }
        RebuildPalette(category);
        if (_isPopulatingEditor)
        {
            return;
        }
        PaletteList.SelectedIndex = 0;
    }

    private void RebuildPalette(string category)
    {
        _palette.Clear();
        foreach (Color color in PanelCladdingMaterialPalette.ColorsFor(category))
        {
            _palette.Add(new MaterialColorChoice(color));
        }
    }

    private void OnPaletteSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingEditor || PaletteList.SelectedItem is not MaterialColorChoice choice)
        {
            return;
        }
        SetSelectedColor(choice.Color, updateText: true);
        bool inUse = _materials.Any(item => item.Color == choice.Color);
        string category = CategorySelect.SelectedItem as string ?? "material";
        ColorAssistText.Text = inUse
            ? "This color is already in use. Choose another variation to keep the legend distinct."
            : $"Suggested variation for {category.ToLowerInvariant()} materials.";
    }

    private void OnHexColorChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _isPopulatingEditor)
        {
            return;
        }
        try
        {
            Color color = PanelCladdingMaterialPalette.ParseColor(HexColorText.Text);
            if (PaletteList.SelectedItem is MaterialColorChoice choice && choice.Color != color)
            {
                PaletteList.SelectedItem = null;
            }
            SetSelectedColor(color, updateText: false);
            ColorAssistText.Text = "Custom color selected.";
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            ColorAssistText.Text = "Enter a six-digit color such as #7B858F.";
        }
    }

    private void OnCustomColorClick(object sender, RoutedEventArgs e)
    {
        if (!_colorPicker.TryPick(this, _selectedColor, out Color selectedColor))
        {
            return;
        }

        PaletteList.SelectedItem = null;
        SetSelectedColor(Color.FromRgb(selectedColor.R, selectedColor.G, selectedColor.B), updateText: true);
        ColorAssistText.Text = "Custom color selected.";
    }

    private void OnConfiguredSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfiguredList.SelectedItem is not PanelCladdingMaterial material)
        {
            UpdateMaterialActionState();
            return;
        }

        _isPopulatingEditor = true;
        try
        {
            string category = EnsureCategory(material.Category);
            CategorySelect.SelectedItem = category;
            CodeText.Text = material.Code;
            NameText.Text = material.Name;
            PaletteList.SelectedItem = _palette.FirstOrDefault(choice => choice.Color == material.Color);
            SetSelectedColor(material.Color, updateText: true);
        }
        finally
        {
            _isPopulatingEditor = false;
        }

        UpdateMaterialActionState();
        ColorAssistText.Text = $"Editing {material.Code}.";
    }

    private void OnCodeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPopulatingEditor)
        {
            return;
        }

        string code = NormalizeCode(CodeText.Text);
        if (ConfiguredList.SelectedItem is PanelCladdingMaterial selected &&
            !string.Equals(selected.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            ConfiguredList.SelectedItem = null;
        }
        UpdateMaterialActionState();
    }

    private void UpdateMaterialActionState()
    {
        if (RemoveMaterialButton is not null)
        {
            RemoveMaterialButton.IsEnabled = ConfiguredList.SelectedItem is PanelCladdingMaterial;
        }
        if (MaterialActionButton is null)
        {
            return;
        }

        string code = NormalizeCode(CodeText.Text);
        bool exists = _materials.Any(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
        MaterialActionButton.Content = exists ? "Save edits" : "Add material";
    }

    private void OnRemoveMaterialClick(object sender, RoutedEventArgs e)
    {
        if (ConfiguredList.SelectedItem is not PanelCladdingMaterial material)
        {
            return;
        }

        _materials.Remove(material);
        ConfiguredList.SelectedItem = null;
        CodeText.Clear();
        NameText.Clear();
        UpdateCatalogueItemWidth();
        UpdateMaterialActionState();
        ColorAssistText.Text = "Select a material to edit, or add a new one.";
        WorkbookStatusText.Text = $"{material.Code} removed from the catalogue. Choose Confirm to save.";
    }

    private void OnAddCategoryClick(object sender, RoutedEventArgs e)
    {
        if (!_categoryPrompt.TryGetCategory(this, _categories, out string category))
        {
            return;
        }

        string selected = EnsureCategory(category);
        CategorySelect.SelectedItem = selected;
        ColorAssistText.Text = $"{selected} category added for this catalogue.";
    }

    private string EnsureCategory(string category)
    {
        string normalized = string.IsNullOrWhiteSpace(category) ? "Composite" : category.Trim();
        string? existing = _categories.FirstOrDefault(item =>
            string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        _categories.Add(normalized);
        return normalized;
    }

    private void SetSelectedColor(Color color, bool updateText)
    {
        _selectedColor = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        CustomColorPreview.Background = brush;
        if (updateText)
        {
            HexColorText.Text = PanelCladdingMaterialPalette.ToHex(color);
        }
    }

    private void OnAddOrSaveMaterialClick(object sender, RoutedEventArgs e)
    {
        string code = NormalizeCode(CodeText.Text);
        string name = NameText.Text.Trim();
        string category = CategorySelect.SelectedItem as string ?? "Composite";
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Enter both a material code and description.", "Material setup",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var updated = new PanelCladdingMaterial
        {
            Code = code,
            Name = name,
            Category = category,
            Color = _selectedColor
        };
        int existingIndex = FindMaterialIndex(code);
        if (existingIndex >= 0)
        {
            _materials[existingIndex] = updated;
            ConfiguredList.SelectedItem = updated;
            ColorAssistText.Text = $"{code} edits saved to the catalogue.";
        }
        else
        {
            _materials.Add(updated);
            ConfiguredList.SelectedItem = updated;
            ColorAssistText.Text = $"{code} added to the catalogue.";
        }
        UpdateCatalogueItemWidth();
        UpdateMaterialActionState();
    }

    private void UpdateCatalogueItemWidth()
    {
        string longestCode = _materials
            .Select(item => item.Code?.Trim() ?? string.Empty)
            .OrderByDescending(code => code.Length)
            .ThenByDescending(code => code, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
        var formatted = new FormattedText(
            longestCode,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            11d,
            Brushes.Black,
            1d);
        CatalogueItemWidth = Math.Max(
            MinimumCatalogueItemWidth,
            Math.Ceiling(formatted.WidthIncludingTrailingWhitespace + CatalogueItemChromeWidth));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private int FindMaterialIndex(string code)
    {
        for (int index = 0; index < _materials.Count; index++)
        {
            if (string.Equals(_materials[index].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        WorkbookPath = WorkbookPathText.Text.Trim();
        if (_controller is not null)
        {
            if (string.IsNullOrWhiteSpace(WorkbookPath))
            {
                MessageBox.Show(this, "Choose a project workbook before saving the material catalogue.",
                    "Material setup", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OperationResponse<PanelCladdingMaterialCatalogSaveResult> response = _controller.SaveMaterialCatalog(
                new PanelCladdingMaterialCatalogSaveRequest
                {
                    WorkbookPath = WorkbookPath,
                    AllowCreate = !File.Exists(WorkbookPath),
                    RemoveLegacyTypeSheets = true,
                    Materials = _materials.Select(item => item.ToCatalogItem()).ToArray()
                });
            if (!response.Success || response.Data is null)
            {
                MessageBox.Show(this, response.Message, "Material setup",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                WorkbookStatusText.Text = response.Message;
                return;
            }
            WorkbookStatusText.Text = $"Saved {response.Data.MaterialCount} project materials.";
        }
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public sealed class MaterialColorChoice
{
    public MaterialColorChoice(Color color)
    {
        Color = color;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Brush = brush;
    }

    public Color Color { get; }
    public Brush Brush { get; }
}
