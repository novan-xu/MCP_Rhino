extern alias rhinocommon;

using Eto.Drawing;
using Eto.Forms;
using System.ComponentModel;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.UI;

public sealed class PanelCladdingEditorWindow : Form
{
    private readonly PanelCladdingEditorController _controller;
    private readonly PanelCladdingPreviewCanvas _preview = new();
    private readonly Label _panelLabel = new();
    private readonly Label _classificationLabel = new();
    private readonly Label _cellLabel = new();
    private readonly Label _keyLabel = new() { TextColor = Colors.Gray };
    private readonly Label _statusLabel = new() { TextColor = Colors.Gray };
    private readonly TextBox _cellEditor = new();
    private readonly TextBox _workbookEditor = new();
    private readonly TextBox _systemEditor = new();
    private readonly Button _previousButton = new() { Text = "Previous" };
    private readonly Button _nextButton = new() { Text = "Next" };
    private readonly Button _browseButton = new() { Text = "Choose Workbook..." };
    private readonly Button _loadSelectedButton = new() { Text = "Load Selected Panel" };
    private readonly Button _reloadButton = new() { Text = "Reload Panel" };
    private readonly Button _closeButton = new() { Text = "Close" };
    private readonly Button _saveButton = new() { Text = "Save Type" };
    private PanelCladdingLayout? _layout;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private int _selectedCellIndex;
    private bool _updatingEditor;
    private bool _dirty;

    public PanelCladdingEditorWindow(PanelCladdingEditorController controller)
    {
        _controller = controller;
        Title = "Panel Cladding Type Editor";
        ClientSize = new Size(960, 760);
        MinimumSize = new Size(720, 580);
        Resizable = true;
        Owner = global::Rhino.UI.RhinoEtoApp.MainWindowForOwner;

        _preview.Height = 440;
        _preview.SelectedCellChanged += (_, key) => SelectCellByKey(key);
        _cellEditor.TextChanged += (_, _) => OnCellValueChanged();
        _cellEditor.KeyDown += OnCellEditorKeyDown;
        _previousButton.Click += (_, _) => MoveSelection(-1);
        _nextButton.Click += (_, _) => MoveSelection(1);
        _browseButton.Click += (_, _) => ChooseWorkbook();
        _loadSelectedButton.Click += (_, _) => LoadSelectedPanel();
        _reloadButton.Click += (_, _) => ReloadPanel();
        _closeButton.Click += (_, _) => Close();
        _saveButton.Click += (_, _) => SaveType();
        _workbookEditor.TextChanged += (_, _) => MarkDirty();
        _systemEditor.TextChanged += (_, _) => MarkDirty();
        Closing += OnWindowClosing;

        var layout = new DynamicLayout { Padding = 12, Spacing = new Size(8, 8) };
        layout.AddRow(new Label { Text = "Panel", Font = new Font(SystemFont.Bold, 10f) }, _panelLabel);
        layout.AddRow(new Label { Text = "Geometry" }, _classificationLabel, null, new Label { Text = "System" }, _systemEditor);
        layout.Add(_preview, yscale: true);
        layout.AddRow(new Label { Text = "Cell", Font = new Font(SystemFont.Bold, 10f) }, _cellLabel, _previousButton, _nextButton);
        layout.AddRow(new Label { Text = "Cladding code" }, _cellEditor);
        layout.AddRow(new Label { Text = "Rhino key" }, _keyLabel);
        layout.AddRow(new Label { Text = "Workbook" }, _workbookEditor, _browseButton);
        layout.AddSeparateRow(_loadSelectedButton, _reloadButton, null, _statusLabel, null, _saveButton, _closeButton);
        Content = layout;
    }

    public event EventHandler? EditorClosed;

    public OperationResponse LoadPanel(string filePath, Guid objectId)
    {
        if (_dirty && !ConfirmDiscardChanges())
        {
            return OperationResponse.Fail("Panel load cancelled; current cell edits were kept.");
        }

        OperationResponse<PanelCladdingLayout> response = _controller.Load(filePath, objectId);
        if (!response.Success || response.Data is null)
        {
            _statusLabel.Text = response.Message;
            _statusLabel.TextColor = Colors.IndianRed;
            return OperationResponse.Fail(response.Message);
        }

        _layout = response.Data;
        _values.Clear();
        foreach (PanelCladdingCell cell in _layout.Cells)
        {
            _values[cell.UserTextKey] = cell.Value;
        }
        _panelLabel.Text = string.IsNullOrWhiteSpace(_layout.ObjectName)
            ? _layout.ObjectId.ToString()
            : $"{_layout.ObjectName} ({_layout.ObjectId})";
        _classificationLabel.Text = $"{_layout.GeometryClass} â€” {_layout.GeometryDiagnostic}";
        _classificationLabel.TextColor = _layout.CanSave ? Colors.DarkGreen : Colors.IndianRed;
        _workbookEditor.Text = _layout.WorkbookPath;
        _systemEditor.Text = _layout.SystemCode;
        _saveButton.Enabled = _layout.CanSave;
        _selectedCellIndex = 0;
        _preview.SetScene(_layout, _controller.Project(_layout), _values);
        UpdateSelectedCellEditor();
        _dirty = false;
        _statusLabel.Text = _layout.CanSave ? "Ready." : "This panel is not reliably projectable.";
        _statusLabel.TextColor = _layout.CanSave ? Colors.Gray : Colors.IndianRed;
        return OperationResponse.Ok();
    }

    protected override void OnClosed(EventArgs e)
    {
        EditorClosed?.Invoke(this, EventArgs.Empty);
        base.OnClosed(e);
    }

    private void OnCellValueChanged()
    {
        if (_updatingEditor || _layout is null || _layout.Cells.Count == 0)
        {
            return;
        }
        PanelCladdingCell cell = OrderedCells()[_selectedCellIndex];
        _values[cell.UserTextKey] = _cellEditor.Text ?? string.Empty;
        _dirty = true;
        _preview.SetValues(_values);
    }

    private void OnCellEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Keys.Tab)
        {
            MoveSelection(e.Modifiers.HasFlag(Keys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Keys.Up)
        {
            MoveGridSelection(0, 1);
            e.Handled = true;
        }
        else if (e.Key == Keys.Down)
        {
            MoveGridSelection(0, -1);
            e.Handled = true;
        }
        else if (e.Key == Keys.Left)
        {
            MoveGridSelection(-1, 0);
            e.Handled = true;
        }
        else if (e.Key == Keys.Right)
        {
            MoveGridSelection(1, 0);
            e.Handled = true;
        }
    }

    private void SelectCellByKey(string key)
    {
        if (_layout is null)
        {
            return;
        }
        int index = OrderedCells().FindIndex(cell => string.Equals(cell.UserTextKey, key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _selectedCellIndex = index;
            UpdateSelectedCellEditor();
        }
    }

    private void MoveSelection(int delta)
    {
        if (_layout is null || _layout.Cells.Count == 0)
        {
            return;
        }
        int count = _layout.Cells.Count;
        _selectedCellIndex = (_selectedCellIndex + delta + count) % count;
        UpdateSelectedCellEditor();
    }

    private void MoveGridSelection(int columnDelta, int rowDelta)
    {
        if (_layout is null || _layout.Cells.Count == 0)
        {
            return;
        }
        PanelCladdingCell current = OrderedCells()[_selectedCellIndex];
        int column = Math.Clamp(current.Column + columnDelta, 0, _layout.ColumnCount - 1);
        int row = Math.Clamp(current.Row + rowDelta, 0, _layout.RowCount - 1);
        int index = OrderedCells().FindIndex(cell => cell.Column == column && cell.Row == row);
        if (index >= 0)
        {
            _selectedCellIndex = index;
            UpdateSelectedCellEditor();
        }
    }

    private void UpdateSelectedCellEditor()
    {
        if (_layout is null || _layout.Cells.Count == 0)
        {
            return;
        }
        PanelCladdingCell cell = OrderedCells()[_selectedCellIndex];
        _updatingEditor = true;
        _cellLabel.Text = cell.ShortLabel;
        _keyLabel.Text = cell.UserTextKey;
        _values.TryGetValue(cell.UserTextKey, out string? value);
        _cellEditor.Text = value ?? string.Empty;
        _updatingEditor = false;
        _preview.SelectCell(cell.UserTextKey);
        _cellEditor.Focus();
        _cellEditor.SelectAll();
    }

    private List<PanelCladdingCell> OrderedCells() => _layout!.Cells
        .OrderBy(cell => cell.Column)
        .ThenBy(cell => cell.Row)
        .ToList();

    private void ChooseWorkbook()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Choose or create the panel cladding type workbook",
            FileName = _workbookEditor.Text,
            CheckFileExists = false
        };
        dialog.Filters.Add(new FileFilter("Excel workbook", ".xlsx"));
        if (dialog.ShowDialog(this) == DialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
        {
            _workbookEditor.Text = dialog.FileName;
        }
    }

    private void ReloadPanel()
    {
        if (_layout is not null)
        {
            LoadPanel(_layout.DocumentPath, _layout.ObjectId);
        }
    }

    private void LoadSelectedPanel()
    {
        if (_layout is null)
        {
            return;
        }
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null || document.RuntimeSerialNumber != _layout.DocumentRuntimeSerialNumber)
        {
            ShowError("The editor is pinned to a different Rhino document.");
            return;
        }
        var selected = document.Objects.GetSelectedObjects(includeLights: false, includeGrips: false)
            .Where(item => item.Geometry is Brep)
            .ToArray();
        if (selected.Length != 1)
        {
            ShowError("Select exactly one panel Brep, then choose Load Selected Panel.");
            return;
        }
        OperationResponse loaded = LoadPanel(_layout.DocumentPath, selected[0].Id);
        if (!loaded.Success)
        {
            _statusLabel.Text = loaded.Message;
        }
    }

    private void MarkDirty()
    {
        if (!_updatingEditor && _layout is not null)
        {
            _dirty = true;
        }
    }

    private bool ConfirmDiscardChanges()
    {
        return MessageBox.Show(
            this,
            "Discard unsaved panel cladding edits?",
            "Panel Cladding Type",
            MessageBoxButtons.YesNo,
            MessageBoxType.Warning) == DialogResult.Yes;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_dirty && !ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void ShowError(string message)
    {
        _statusLabel.Text = message;
        _statusLabel.TextColor = Colors.IndianRed;
        MessageBox.Show(this, message, "Panel Cladding Type", MessageBoxType.Error);
    }

    private void SaveType()
    {
        if (_layout is null)
        {
            return;
        }
        string workbookPath = _workbookEditor.Text?.Trim() ?? string.Empty;
        bool allowCreate = !string.IsNullOrWhiteSpace(workbookPath) && !File.Exists(workbookPath);
        _saveButton.Enabled = false;
        _statusLabel.Text = "Saving...";
        _statusLabel.TextColor = Colors.Gray;
        try
        {
            OperationResponse<PanelCladdingSaveResult> response = _controller.Save(new PanelCladdingSaveRequest
            {
                FilePath = _layout.DocumentPath,
                ObjectId = _layout.ObjectId,
                ExpectedGeometryFingerprint = _layout.GeometryFingerprint,
                WorkbookPath = workbookPath,
                AllowCreateWorkbook = allowCreate,
                SystemCode = _systemEditor.Text ?? string.Empty,
                CellValues = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase)
            });
            if (!response.Success || response.Data is null)
            {
                _statusLabel.Text = response.Message;
                _statusLabel.TextColor = Colors.IndianRed;
                MessageBox.Show(this, response.Message, "Panel Cladding Type", MessageBoxType.Error);
                return;
            }

            _statusLabel.Text = $"Saved {response.Data.TypeCode} to {response.Data.SheetName}.";
            _statusLabel.TextColor = Colors.DarkGreen;
            MessageBox.Show(this, _statusLabel.Text, "Panel Cladding Type", MessageBoxType.Information);
            _dirty = false;
            LoadPanel(_layout.DocumentPath, _layout.ObjectId);
        }
        finally
        {
            _saveButton.Enabled = _layout.CanSave;
        }
    }
}

