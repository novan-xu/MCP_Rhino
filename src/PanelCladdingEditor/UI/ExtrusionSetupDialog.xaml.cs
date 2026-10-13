using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public partial class ExtrusionSetupDialog : Window
{
    private readonly PanelCladdingEditorController? _controller;
    private readonly IExtrusionCatalogueClearPrompt _clearPrompt;
    private readonly ObservableCollection<PanelFrameExtrusion> _extrusions;
    private readonly ListCollectionView _poolView;
    private readonly ListCollectionView _readyView;
    private PanelFrameExtrusion? _selected;
    private bool _loadingEditor;

    public ExtrusionSetupDialog(IEnumerable<PanelFrameExtrusion> extrusions)
        : this(null, string.Empty, extrusions)
    {
    }

    public ExtrusionSetupDialog(
        PanelCladdingEditorController? controller,
        string workbookPath,
        IEnumerable<PanelFrameExtrusion> extrusions)
        : this(controller, workbookPath, extrusions, new ExtrusionCatalogueClearPrompt())
    {
    }

    public ExtrusionSetupDialog(
        PanelCladdingEditorController? controller,
        string workbookPath,
        IEnumerable<PanelFrameExtrusion> extrusions,
        IExtrusionCatalogueClearPrompt clearPrompt)
    {
        InitializeComponent();
        _controller = controller;
        _clearPrompt = clearPrompt ?? throw new ArgumentNullException(nameof(clearPrompt));
        _extrusions = new ObservableCollection<PanelFrameExtrusion>(extrusions.Select(item => item.Copy()));
        Extrusions = _extrusions;
        _poolView = new ListCollectionView(_extrusions) { Filter = item => item is PanelFrameExtrusion profile && !profile.IsConfigured };
        _readyView = new ListCollectionView(_extrusions) { Filter = item => item is PanelFrameExtrusion profile && profile.IsConfigured };
        _poolView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PanelFrameExtrusion.Category)));
        _readyView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PanelFrameExtrusion.Category)));
        _poolView.SortDescriptions.Add(new SortDescription(nameof(PanelFrameExtrusion.SourcePageNumber), ListSortDirection.Ascending));
        _poolView.SortDescriptions.Add(new SortDescription(nameof(PanelFrameExtrusion.BaseCode), ListSortDirection.Ascending));
        _readyView.SortDescriptions.Add(new SortDescription(nameof(PanelFrameExtrusion.SourcePageNumber), ListSortDirection.Ascending));
        _readyView.SortDescriptions.Add(new SortDescription(nameof(PanelFrameExtrusion.BaseCode), ListSortDirection.Ascending));
        PoolList.ItemsSource = _poolView;
        ReadyList.ItemsSource = _readyView;
        WorkbookPath = workbookPath;
        WorkbookPathText.Text = workbookPath;
        PdfPathText.Text = _extrusions.FirstOrDefault()?.SourcePdfPath ?? string.Empty;
        RefreshViews();
        if (!string.IsNullOrWhiteSpace(workbookPath) && File.Exists(workbookPath))
        {
            LoadCatalog(workbookPath);
        }
    }

    public IReadOnlyList<PanelFrameExtrusion> Extrusions { get; }
    public string WorkbookPath { get; private set; } = string.Empty;

    private void OnBrowseWorkbookClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose or create the project workbook",
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
    }

    private void OnBrowsePdfClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose the project extrusion schedule",
            Filter = "PDF drawing (*.pdf)|*.pdf",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            PdfPathText.Text = dialog.FileName;
        }
    }

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        if (_controller is null)
        {
            ImportStatusText.Text = "PDF extraction is not available in this preview.";
            return;
        }
        ExtractButton.IsEnabled = false;
        ImportStatusText.Text = "Reading every PDF schedule page and rendering profile previews...";
        try
        {
            OperationResponse<PanelFrameExtrusionScheduleImportResult> response =
                _controller.ImportFrameExtrusionSchedule(new PanelFrameExtrusionScheduleImportRequest
                {
                    PdfPath = PdfPathText.Text.Trim()
                });
            if (!response.Success || response.Data is null)
            {
                ShowImportError(response.Message);
                return;
            }
            var configured = _extrusions.Where(item => item.IsConfigured)
                .ToDictionary(item => item.SourceCode, StringComparer.OrdinalIgnoreCase);
            _extrusions.Clear();
            foreach (PanelFrameExtrusionCatalogItem item in response.Data.Extrusions)
            {
                PanelFrameExtrusion imported = PanelFrameExtrusion.FromCatalogItem(item);
                _extrusions.Add(configured.TryGetValue(imported.SourceCode, out PanelFrameExtrusion? prior)
                    ? CarryConfiguration(imported, prior)
                    : imported);
            }
            string pages = string.Join(", ", response.Data.ImportedPageNumbers);
            ImportStatusText.Text = $"Imported {_extrusions.Count} profiles from all schedule pages ({pages}).";
            ClearEditor();
            RefreshViews();
        }
        finally
        {
            ExtractButton.IsEnabled = true;
        }
    }

    private void LoadCatalog(string workbookPath)
    {
        if (_controller is null)
        {
            return;
        }
        OperationResponse<PanelFrameExtrusionCatalog> response = _controller.LoadFrameExtrusionCatalog(workbookPath);
        if (!response.Success || response.Data is null)
        {
            ImportStatusText.Text = response.Message;
            return;
        }
        _extrusions.Clear();
        foreach (PanelFrameExtrusionCatalogItem item in response.Data.Extrusions)
        {
            _extrusions.Add(PanelFrameExtrusion.FromCatalogItem(item));
        }
        PdfPathText.Text = _extrusions.FirstOrDefault()?.SourcePdfPath ?? string.Empty;
        ImportStatusText.Text = _extrusions.Count == 0
            ? "This workbook does not yet contain an extrusion catalogue."
            : $"Loaded {_extrusions.Count} project profiles from the workbook.";
        ClearEditor();
        RefreshViews();
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingEditor)
        {
            return;
        }
        var list = (ListBox)sender;
        if (list.SelectedItem is not PanelFrameExtrusion profile)
        {
            return;
        }
        _loadingEditor = true;
        if (ReferenceEquals(list, PoolList))
        {
            ReadyList.SelectedItem = null;
        }
        else
        {
            PoolList.SelectedItem = null;
        }
        LoadEditor(profile);
        _loadingEditor = false;
    }

    private void LoadEditor(PanelFrameExtrusion profile)
    {
        _selected = profile;
        EditorPreview.Source = profile.Thumbnail;
        EditorCodeText.Text = profile.IsConfigured ? profile.Code : profile.BaseCode;
        EditorDescriptionText.Text = $"{profile.Category} · page {profile.SourcePageNumber}" +
                                     (string.IsNullOrWhiteSpace(profile.Description) ? string.Empty : $" · {profile.Description}");
        DimensionTabs.SelectedIndex = profile.Dimension == PanelFrameProfileDimension.ZeroDimensional ? 1 : 0;
        QuantityText.Text = profile.Dimension == PanelFrameProfileDimension.OneDimensional &&
                            profile.CalculationValue is > 1d
            ? profile.CalculationValue.Value.ToString("0", CultureInfo.InvariantCulture)
            : string.Empty;
        FixedRadio.IsChecked = profile.Calculation != PanelFrameProfileCalculation.Spacing;
        SpacingRadio.IsChecked = profile.Calculation == PanelFrameProfileCalculation.Spacing;
        ZeroValueText.Text = profile.Dimension == PanelFrameProfileDimension.ZeroDimensional &&
                             profile.CalculationValue is double value
            ? value.ToString("0.#####", CultureInfo.InvariantCulture)
            : string.Empty;
        RefreshParentCodes(profile.ParentCode);
        ConfigureButton.IsEnabled = true;
        ConfigureButton.Content = profile.IsConfigured ? "Update ready profile" : "Move to ready catalogue";
        ReturnButton.IsEnabled = profile.IsConfigured;
        UpdateZeroValueLabel();
    }

    private void OnDimensionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selected is not null)
        {
            EditorCodeText.Text = $"{(DimensionTabs.SelectedIndex == 0 ? "1D" : "0D")}-{_selected.BaseCode}";
        }
    }

    private void OnCalculationModeChanged(object sender, RoutedEventArgs e) => UpdateZeroValueLabel();

    private void UpdateZeroValueLabel()
    {
        if (ZeroValueLabel is not null)
        {
            ZeroValueLabel.Text = SpacingRadio?.IsChecked == true ? "SPACING (INCHES)" : "QUANTITY";
        }
    }

    private void OnConfigureClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }
        PanelFrameProfileDimension dimension = DimensionTabs.SelectedIndex == 0
            ? PanelFrameProfileDimension.OneDimensional
            : PanelFrameProfileDimension.ZeroDimensional;
        PanelFrameProfileCalculation calculation;
        double? calculationValue;
        if (dimension == PanelFrameProfileDimension.OneDimensional)
        {
            calculation = PanelFrameProfileCalculation.Length;
            if (string.IsNullOrWhiteSpace(QuantityText.Text))
            {
                calculationValue = 1d;
            }
            else if (!TryPositiveInteger(QuantityText.Text, out double quantity))
            {
                ShowConfigurationError("Quantity must be a positive whole number. Leave it blank for one piece.");
                return;
            }
            else
            {
                calculationValue = quantity;
            }
        }
        else
        {
            calculation = SpacingRadio.IsChecked == true
                ? PanelFrameProfileCalculation.Spacing
                : PanelFrameProfileCalculation.FixedQuantity;
            if (!double.TryParse(ZeroValueText.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out double value) || !double.IsFinite(value) || value <= 0d ||
                (calculation == PanelFrameProfileCalculation.FixedQuantity &&
                 Math.Abs(value - Math.Round(value)) > 1e-9d))
            {
                ShowConfigurationError(calculation == PanelFrameProfileCalculation.Spacing
                    ? "Spacing must be a positive number of inches."
                    : "Fixed quantity must be a positive whole number.");
                return;
            }
            calculationValue = value;
        }

        string code = $"{(dimension == PanelFrameProfileDimension.OneDimensional ? "1D" : "0D")}-{_selected.BaseCode}";
        if (_extrusions.Any(item => !ReferenceEquals(item, _selected) &&
                                    string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            ShowConfigurationError($"The ready catalogue already contains {code}.");
            return;
        }
        var replacement = new PanelFrameExtrusion
        {
            Code = code,
            BaseCode = _selected.BaseCode,
            SourceCode = _selected.SourceCode,
            Description = _selected.Description,
            Category = _selected.Category,
            Dimension = dimension,
            Calculation = calculation,
            CalculationValue = calculationValue,
            ParentCode = ParentCodeCombo.SelectedItem as string ?? string.Empty,
            SourcePdfPath = _selected.SourcePdfPath,
            SourcePageNumber = _selected.SourcePageNumber,
            ThumbnailPng = _selected.ThumbnailPng.ToArray(),
            Thumbnail = _selected.Thumbnail
        };
        ReplaceSelected(replacement);
    }

    private void OnReturnToPoolClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null || !_selected.IsConfigured)
        {
            return;
        }
        PanelFrameExtrusion? dependent = _extrusions.FirstOrDefault(item =>
            string.Equals(item.ParentCode, _selected.Code, StringComparison.OrdinalIgnoreCase));
        if (dependent is not null)
        {
            ShowConfigurationError($"{_selected.Code} is the parent of {dependent.Code}. Clear that dependency first.");
            return;
        }
        ReplaceSelected(new PanelFrameExtrusion
        {
            BaseCode = _selected.BaseCode,
            SourceCode = _selected.SourceCode,
            Description = _selected.Description,
            Category = _selected.Category,
            SourcePdfPath = _selected.SourcePdfPath,
            SourcePageNumber = _selected.SourcePageNumber,
            ThumbnailPng = _selected.ThumbnailPng.ToArray(),
            Thumbnail = _selected.Thumbnail
        });
    }

    private void ReplaceSelected(PanelFrameExtrusion replacement)
    {
        if (_selected is null)
        {
            return;
        }
        int index = _extrusions.IndexOf(_selected);
        _extrusions[index] = replacement;
        _selected = replacement;
        RefreshViews();
        LoadEditor(replacement);
        if (replacement.IsConfigured)
        {
            ReadyList.SelectedItem = replacement;
        }
        else
        {
            PoolList.SelectedItem = replacement;
        }
    }

    private void RefreshViews()
    {
        _poolView.Refresh();
        _readyView.Refresh();
        int poolCount = _extrusions.Count(item => !item.IsConfigured);
        int readyCount = _extrusions.Count - poolCount;
        PoolCountText.Text = poolCount == 1 ? "1 profile awaiting setup" : $"{poolCount} profiles awaiting setup";
        ReadyCountText.Text = readyCount == 1 ? "1 profile ready to drag" : $"{readyCount} profiles ready to drag";
        RefreshParentCodes(_selected?.ParentCode ?? string.Empty);
    }

    private void RefreshParentCodes(string selectedCode)
    {
        string[] parents = new[] { string.Empty }.Concat(_extrusions
                .Where(item => item.IsConfigured && !ReferenceEquals(item, _selected))
                .Select(item => item.Code)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        ParentCodeCombo.ItemsSource = parents;
        ParentCodeCombo.SelectedItem = parents.FirstOrDefault(code =>
            string.Equals(code, selectedCode, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private void ClearEditor()
    {
        _selected = null;
        PoolList.SelectedItem = null;
        ReadyList.SelectedItem = null;
        EditorPreview.Source = null;
        EditorCodeText.Text = "Select a profile";
        EditorDescriptionText.Text = string.Empty;
        DimensionTabs.SelectedIndex = 0;
        QuantityText.Clear();
        FixedRadio.IsChecked = true;
        ZeroValueText.Clear();
        ConfigureButton.IsEnabled = false;
        ConfigureButton.Content = "Move to ready catalogue";
        ReturnButton.IsEnabled = false;
        RefreshParentCodes(string.Empty);
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        if (!_clearPrompt.ConfirmClear(this, _extrusions.Count))
        {
            return;
        }

        _extrusions.Clear();
        PdfPathText.Clear();
        ClearEditor();
        RefreshViews();
        ImportStatusText.Text = "Catalogue cleared. Choose another PDF to extract, or Confirm to save.";
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        WorkbookPath = WorkbookPathText.Text.Trim();
        if (string.IsNullOrWhiteSpace(WorkbookPath))
        {
            ShowConfigurationError("Choose a project workbook before saving the extrusion catalogue.");
            return;
        }
        string? dependencyError = ValidateDependencies();
        if (dependencyError is not null)
        {
            ShowConfigurationError(dependencyError);
            return;
        }
        if (_controller is not null)
        {
            OperationResponse<PanelFrameExtrusionCatalogSaveResult> response =
                _controller.SaveFrameExtrusionCatalog(new PanelFrameExtrusionCatalogSaveRequest
                {
                    WorkbookPath = WorkbookPath,
                    AllowCreate = !File.Exists(WorkbookPath),
                    Extrusions = _extrusions.Select(item => item.ToCatalogItem()).ToArray()
                });
            if (!response.Success || response.Data is null)
            {
                ShowImportError(response.Message);
                return;
            }
        }
        DialogResult = true;
        Close();
    }

    private string? ValidateDependencies()
    {
        var ready = _extrusions.Where(item => item.IsConfigured)
            .ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        foreach (PanelFrameExtrusion profile in ready.Values)
        {
            if (!string.IsNullOrWhiteSpace(profile.ParentCode) && !ready.ContainsKey(profile.ParentCode))
            {
                return $"Parent {profile.ParentCode} for {profile.Code} is not in the ready catalogue.";
            }
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.Code };
            PanelFrameExtrusion current = profile;
            while (!string.IsNullOrWhiteSpace(current.ParentCode))
            {
                if (!visited.Add(current.ParentCode))
                {
                    return $"Dependency cycle detected at {current.ParentCode}.";
                }
                current = ready[current.ParentCode];
            }
        }
        return null;
    }

    private static PanelFrameExtrusion CarryConfiguration(PanelFrameExtrusion imported, PanelFrameExtrusion prior) => new()
    {
        Code = $"{(prior.Dimension == PanelFrameProfileDimension.ZeroDimensional ? "0D" : "1D")}-{imported.BaseCode}",
        BaseCode = imported.BaseCode,
        SourceCode = imported.SourceCode,
        Description = imported.Description,
        Category = imported.Category,
        Dimension = prior.Dimension,
        Calculation = prior.Calculation,
        CalculationValue = prior.CalculationValue,
        ParentCode = prior.ParentCode,
        SourcePdfPath = imported.SourcePdfPath,
        SourcePageNumber = imported.SourcePageNumber,
        ThumbnailPng = imported.ThumbnailPng.ToArray(),
        Thumbnail = imported.Thumbnail
    };

    private static bool TryPositiveInteger(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
        value >= 1d && value <= 100_000d;

    private void ShowImportError(string message)
    {
        ImportStatusText.Text = message;
        MessageBox.Show(this, message, "Extrusion setup", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ShowConfigurationError(string message) =>
        MessageBox.Show(this, message, "Extrusion setup", MessageBoxButton.OK, MessageBoxImage.Information);

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
