extern alias rhinocommon;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public partial class PanelCladdingEditorWindow : Window
{
    private const string FrameExtrusionDragFormat = "PanelCladdingEditor.FrameExtrusionCode";
    private readonly PanelCladdingEditorController _controller;
    private readonly PanelCladdingLogicalCellService _logicalCells = new();
    private readonly PanelCladdingTopologyNormalizationService _topologyNormalizer =
        new(new PanelCladdingKeyService());
    private readonly ObservableCollection<PanelCladdingMaterial> _materials = [];
    private readonly ObservableCollection<PanelFrameExtrusion> _frameExtrusions = [];
    private readonly ObservableCollection<PanelFrameExtrusion> _availableFrameExtrusions = [];
    private readonly Dictionary<string, HashSet<string>> _frameAssignmentCodes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PanelFrameProfileDefinition> _frameDefinitions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _frameCurveModifiers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedExtrusions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PanelExtrusionMergeGroup> _mergedGroups = [];
    private readonly HashSet<string> _deletedExtrusions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenExtrusions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<double> _horizontalOffsets = [];
    private readonly List<double> _verticalOffsets = [];
    private readonly List<bool> _rowLocks = [];
    private readonly List<bool> _columnLocks = [];
    private readonly Stack<EditorSnapshot> _undoStack = new();
    private readonly DispatcherTimer _toastTimer;
    private PanelCladdingLayout? _sourceLayout;
    private PanelCladdingLayout? _layout;
    private PanelEditorView _activeView = PanelEditorView.Cladding;
    private PanelExtrusionAxis? _placementAxis;
    private bool _dirty;
    private bool _structuralDirty;
    private bool _claddingDirty;
    private bool _isLoading;
    private bool _syncingAssignmentControls;
    private bool _committingDividerOffset;
    private Point _materialDragStart;
    private Point _extrusionDragStart;
    private string _workbookPath = string.Empty;

    public PanelCladdingEditorWindow(PanelCladdingEditorController controller)
    {
        _controller = controller;
        InitializeComponent();
        MaterialLegend.ItemsSource = _materials;
        MaterialSelect.ItemsSource = _materials;
        ExtrusionLegend.ItemsSource = _availableFrameExtrusions;
        Closing += OnWindowClosing;
        PreviewKeyDown += OnWindowPreviewKeyDown;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6d) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        };
    }

    public event EventHandler? EditorClosed;

    public OperationResponse LoadPanel(string filePath, Guid objectId)
    {
        if (_dirty && !ConfirmDiscardChanges())
        {
            return OperationResponse.Fail("Panel load cancelled; current edits were kept.");
        }
        OperationResponse<PanelCladdingLayout> response = _controller.Load(filePath, objectId);
        if (!response.Success || response.Data is null)
        {
            SetStatus(response.Message, error: true);
            return OperationResponse.Fail(response.Message);
        }

        _isLoading = true;
        try
        {
            _sourceLayout = response.Data;
            _horizontalOffsets.Clear();
            _horizontalOffsets.AddRange(_sourceLayout.HorizontalOffsets.OrderBy(value => value));
            _verticalOffsets.Clear();
            _verticalOffsets.AddRange(_sourceLayout.VerticalOffsets.OrderBy(value => value));
            _rowLocks.Clear();
            _rowLocks.AddRange(Enumerable.Repeat(false, _horizontalOffsets.Count + 1));
            _columnLocks.Clear();
            _columnLocks.AddRange(Enumerable.Repeat(false, _verticalOffsets.Count + 1));
            _values.Clear();
            foreach (PanelCladdingCell cell in _sourceLayout.Cells)
            {
                _values[cell.UserTextKey] = cell.Value;
            }
            _workbookPath = _sourceLayout.WorkbookPath;
            LoadProjectMaterials();
            LoadProjectExtrusions();
            _undoStack.Clear();
            _mergedGroups.Clear();
            _deletedExtrusions.Clear();
            _hiddenExtrusions.Clear();
            _selectedExtrusions.Clear();
            _selectedKeys.Clear();
            ApplyTopologyState(_sourceLayout.Topology);
            ApplyFrameAssignmentState(_sourceLayout.FrameAssignments);
            _layout = BuildWorkingLayout();
            int collapsedTracks = NormalizeFullyDeletedTracks();
            PanelCladdingCell? initial = _layout.Cells
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .Skip(Math.Min(1, Math.Max(0, _layout.Cells.Count - 1)))
                .FirstOrDefault();
            if (initial is not null)
            {
                _selectedKeys.Add(initial.UserTextKey);
            }
            _structuralDirty = collapsedTracks > 0;
            _claddingDirty = collapsedTracks > 0;
            EnsureMaterialLibrary();
            EnsureFrameExtrusionLibrary();
            SwitchView(PanelEditorView.Cladding, showNotice: false);
            PopulatePanelMetadata();
            RefreshCanvas();
            UpdateSelectionUi(syncAssignmentMode: true);
            RefreshDirtyState();
            SetStatus(
                !_layout.CanSave
                    ? "This panel is not reliably projectable."
                    : collapsedTracks > 0
                        ? $"{collapsedTracks} empty extrusion {(collapsedTracks == 1 ? "track was" : "tracks were")} normalized · Save to update panel attributes"
                        : "Grid metadata loaded",
                !_layout.CanSave);
        }
        finally
        {
            _isLoading = false;
        }
        return OperationResponse.Ok();
    }

    protected override void OnClosed(EventArgs e)
    {
        EditorClosed?.Invoke(this, EventArgs.Empty);
        base.OnClosed(e);
    }

    private PanelCladdingLayout BuildWorkingLayout()
    {
        PanelCladdingLayout source = _sourceLayout ?? throw new InvalidOperationException("A source panel must be loaded first.");
        int rows = _horizontalOffsets.Count + 1;
        int columns = _verticalOffsets.Count + 1;
        var cells = new List<PanelCladdingCell>(rows * columns);
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
            {
                string rowLabel = PanelCladdingKeyService.GetRowLabel(row);
                string key = PanelCladdingKeyService.GetCellKey(column, rowLabel);
                cells.Add(new PanelCladdingCell
                {
                    Column = column,
                    Row = row,
                    RowLabel = rowLabel,
                    ShortLabel = $"{column}{rowLabel}",
                    UserTextKey = key,
                    Value = _values.GetValueOrDefault(key, string.Empty)
                });
            }
        }
        return new PanelCladdingLayout
        {
            ObjectId = source.ObjectId,
            DocumentRuntimeSerialNumber = source.DocumentRuntimeSerialNumber,
            DocumentPath = source.DocumentPath,
            ObjectName = source.ObjectName,
            LayerFullPath = source.LayerFullPath,
            SystemCode = source.SystemCode,
            GeometryFingerprint = source.GeometryFingerprint,
            GeometryClass = source.GeometryClass,
            GeometryDiagnostic = source.GeometryDiagnostic,
            Width = source.Width,
            Height = source.Height,
            ModelTolerance = source.ModelTolerance,
            ModelUnitScaleToMillimeters = source.ModelUnitScaleToMillimeters,
            HorizontalOffsets = _horizontalOffsets.ToArray(),
            VerticalOffsets = _verticalOffsets.ToArray(),
            Cells = cells,
            Topology = CaptureTopologyState(),
            FrameAssignments = CaptureFrameAssignmentState(),
            SourceUserText = source.SourceUserText,
            Preview = source.Preview,
            WorkbookPath = source.WorkbookPath
        };
    }

    private PanelCladdingTopologyState CaptureTopologyState()
    {
        var missing = new List<PanelCladdingSegmentCoordinate>();
        foreach (string id in _deletedExtrusions)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(id, out PanelExtrusionAxis axis, out double offset, out int bay))
            {
                continue;
            }
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            int track = IndexOfOffset(offsets, offset);
            if (track >= 0)
            {
                missing.Add(new PanelCladdingSegmentCoordinate(ToTopologyAxis(axis), track, bay));
            }
        }

        var runs = new List<PanelCladdingMergeRun>();
        foreach (PanelExtrusionMergeGroup group in _mergedGroups)
        {
            IReadOnlyList<double> offsets = group.Axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            int track = IndexOfOffset(offsets, group.Offset);
            if (track < 0)
            {
                continue;
            }
            int[] bays = group.AtomicIds
                .Select(id => PanelExtrusionTopology.TryParseAtomicId(id, out _, out _, out int bay) ? bay : -1)
                .Where(bay => bay >= 0)
                .Distinct()
                .OrderBy(bay => bay)
                .ToArray();
            if (bays.Length < 2)
            {
                continue;
            }
            int start = bays[0];
            int previous = bays[0];
            foreach (int bay in bays.Skip(1))
            {
                if (bay != previous + 1)
                {
                    if (previous > start)
                    {
                        runs.Add(new PanelCladdingMergeRun(ToTopologyAxis(group.Axis), track, start, previous));
                    }
                    start = bay;
                }
                previous = bay;
            }
            if (previous > start)
            {
                runs.Add(new PanelCladdingMergeRun(ToTopologyAxis(group.Axis), track, start, previous));
            }
        }
        return new PanelCladdingTopologyState
        {
            MissingSegments = missing
                .Distinct()
                .OrderBy(item => item.Axis)
                .ThenBy(item => item.Track)
                .ThenBy(item => item.Bay)
                .ToArray(),
            HiddenSegments = CaptureSegmentCoordinates(_hiddenExtrusions),
            MergeRuns = runs
                .OrderBy(item => item.Axis)
                .ThenBy(item => item.Track)
                .ThenBy(item => item.StartBay)
                .ToArray()
        };
    }

    private void ApplyTopologyState(PanelCladdingTopologyState topology)
    {
        foreach (PanelCladdingSegmentCoordinate segment in topology.MissingSegments)
        {
            PanelExtrusionAxis axis = FromTopologyAxis(segment.Axis);
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            if (segment.Track >= 0 && segment.Track < offsets.Count)
            {
                _deletedExtrusions.Add(PanelExtrusionTopology.AtomicId(axis, offsets[segment.Track], segment.Bay));
            }
        }
        foreach (PanelCladdingSegmentCoordinate segment in topology.HiddenSegments)
        {
            PanelExtrusionAxis axis = FromTopologyAxis(segment.Axis);
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            if (segment.Track >= 0 && segment.Track < offsets.Count)
            {
                _hiddenExtrusions.Add(PanelExtrusionTopology.AtomicId(axis, offsets[segment.Track], segment.Bay));
            }
        }
        foreach (PanelCladdingMergeRun run in topology.MergeRuns)
        {
            PanelExtrusionAxis axis = FromTopologyAxis(run.Axis);
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            if (run.Track < 0 || run.Track >= offsets.Count || run.EndBay <= run.StartBay)
            {
                continue;
            }
            double offset = offsets[run.Track];
            string[] atomicIds = Enumerable.Range(run.StartBay, run.EndBay - run.StartBay + 1)
                .Select(bay => PanelExtrusionTopology.AtomicId(axis, offset, bay))
                .ToArray();
            string id = $"M:{axis}:{offset.ToString("0.#####", CultureInfo.InvariantCulture)}:{string.Join('+', atomicIds)}";
            _mergedGroups.Add(new PanelExtrusionMergeGroup(id, axis, offset, atomicIds));
        }
    }

    private PanelFrameAssignmentState CaptureFrameAssignmentState()
    {
        var frames = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var segments = new List<PanelFrameSegmentAssignment>();
        foreach ((string key, HashSet<string> values) in _frameAssignmentCodes)
        {
            string[] codes = values
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            if (codes.Length == 0)
            {
                continue;
            }
            if (key.StartsWith("FRM_", StringComparison.OrdinalIgnoreCase))
            {
                frames[key.ToUpperInvariant()] = codes;
            }
            else if (PanelFrameAssignmentService.TryParseSegmentKey(key, out PanelCladdingSegmentCoordinate segment))
            {
                segments.Add(new PanelFrameSegmentAssignment(segment, codes));
            }
        }
        return new PanelFrameAssignmentState
        {
            FrameAssignments = frames,
            SegmentAssignments = segments
                .OrderBy(item => item.Segment.Axis)
                .ThenBy(item => item.Segment.Track)
                .ThenBy(item => item.Segment.Bay)
                .ToArray(),
            Definitions = _frameDefinitions.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase),
            CurveModifiers = _frameCurveModifiers.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private void ApplyFrameAssignmentState(PanelFrameAssignmentState state)
    {
        _frameAssignmentCodes.Clear();
        _frameDefinitions.Clear();
        _frameCurveModifiers.Clear();
        foreach ((string frame, IReadOnlyList<string> codes) in state.FrameAssignments)
        {
            _frameAssignmentCodes[frame.ToUpperInvariant()] = codes
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        foreach (PanelFrameSegmentAssignment assignment in state.SegmentAssignments)
        {
            _frameAssignmentCodes[PanelFrameAssignmentService.SegmentKey(assignment.Segment)] =
                assignment.Codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        foreach ((string code, PanelFrameProfileDefinition definition) in state.Definitions)
        {
            _frameDefinitions[code] = definition;
        }
        foreach ((string key, double modifier) in state.CurveModifiers)
        {
            _frameCurveModifiers[key] = modifier;
        }
    }

    private void RemoveFrameAssignments(IEnumerable<string> atomicIds)
    {
        foreach (string id in atomicIds)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(
                    id,
                    out PanelExtrusionAxis axis,
                    out double offset,
                    out int bay))
            {
                continue;
            }
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            int track = IndexOfOffset(offsets, offset);
            if (track >= 0)
            {
                string key = PanelFrameAssignmentService.SegmentKey(
                    new PanelCladdingSegmentCoordinate(ToTopologyAxis(axis), track, bay));
                _frameAssignmentCodes.Remove(key);
                _frameCurveModifiers.Remove(key);
            }
        }
    }

    private void RemapFrameAssignmentsForInsertedMullion(
        PanelCladdingTopologyAxis insertedAxis,
        int splitIndex) =>
        RemapFrameAssignments(segment =>
        {
            if (segment.Axis == insertedAxis)
            {
                return [segment with { Track = segment.Track < splitIndex ? segment.Track : segment.Track + 1 }];
            }
            if (segment.Bay < splitIndex)
            {
                return [segment];
            }
            if (segment.Bay > splitIndex)
            {
                return [segment with { Bay = segment.Bay + 1 }];
            }
            return [segment, segment with { Bay = segment.Bay + 1 }];
        });

    private void RemapFrameAssignmentsForCollapsedTrack(PanelCladdingCollapsedTrack collapsed) =>
        RemapFrameAssignments(segment =>
        {
            if (segment.Axis == collapsed.Axis)
            {
                if (segment.Track == collapsed.Track)
                {
                    return [];
                }
                return [segment with
                {
                    Track = segment.Track < collapsed.Track ? segment.Track : segment.Track - 1
                }];
            }
            return [segment with
            {
                Bay = segment.Bay <= collapsed.Track ? segment.Bay : segment.Bay - 1
            }];
        });

    private void RemapFrameAssignments(
        Func<PanelCladdingSegmentCoordinate, IEnumerable<PanelCladdingSegmentCoordinate>> map)
    {
        var remapped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var remappedModifiers = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, HashSet<string> codes) in _frameAssignmentCodes)
        {
            if (key.StartsWith("FRM_", StringComparison.OrdinalIgnoreCase))
            {
                remapped[key] = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (_frameCurveModifiers.TryGetValue(key, out double frameModifier))
                {
                    remappedModifiers[key] = frameModifier;
                }
                continue;
            }
            if (!PanelFrameAssignmentService.TryParseSegmentKey(key, out PanelCladdingSegmentCoordinate segment))
            {
                continue;
            }
            foreach (PanelCladdingSegmentCoordinate mapped in map(segment))
            {
                string mappedKey = PanelFrameAssignmentService.SegmentKey(mapped);
                if (!remapped.TryGetValue(mappedKey, out HashSet<string>? mappedCodes))
                {
                    mappedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    remapped[mappedKey] = mappedCodes;
                }
                mappedCodes.UnionWith(codes);
                if (_frameCurveModifiers.TryGetValue(key, out double modifier))
                {
                    if (remappedModifiers.TryGetValue(mappedKey, out double prior) &&
                        Math.Abs(prior - modifier) > 1e-9d)
                    {
                        remappedModifiers.Remove(mappedKey);
                    }
                    else
                    {
                        remappedModifiers[mappedKey] = modifier;
                    }
                }
            }
        }
        _frameAssignmentCodes.Clear();
        foreach ((string key, HashSet<string> codes) in remapped)
        {
            _frameAssignmentCodes[key] = codes;
        }
        _frameCurveModifiers.Clear();
        foreach ((string key, double modifier) in remappedModifiers)
        {
            _frameCurveModifiers[key] = modifier;
        }
    }

    private IReadOnlyList<string> AssignmentKeys(PanelExtrusionSegment segment)
    {
        if (segment.IsFrame)
        {
            return [segment.Id.ToUpperInvariant()];
        }
        var result = new List<string>();
        foreach (string atomicId in segment.AtomicIds)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(
                    atomicId,
                    out PanelExtrusionAxis axis,
                    out double offset,
                    out int bay))
            {
                continue;
            }
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            int track = IndexOfOffset(offsets, offset);
            if (track >= 0)
            {
                result.Add(PanelFrameAssignmentService.SegmentKey(
                    new PanelCladdingSegmentCoordinate(ToTopologyAxis(axis), track, bay)));
            }
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<string> AssignedCodes(PanelExtrusionSegment segment) =>
        AssignmentKeys(segment)
            .SelectMany(key => _frameAssignmentCodes.GetValueOrDefault(
                key,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

    private IReadOnlyDictionary<string, IReadOnlyList<string>> VisibleAssignmentMap() =>
        CurrentSegments().ToDictionary(
            segment => segment.Id,
            AssignedCodes,
            StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<PanelCladdingSegmentCoordinate> CaptureSegmentCoordinates(IEnumerable<string> ids)
    {
        var coordinates = new List<PanelCladdingSegmentCoordinate>();
        foreach (string id in ids)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(id, out PanelExtrusionAxis axis, out double offset, out int bay))
            {
                continue;
            }
            IReadOnlyList<double> offsets = axis == PanelExtrusionAxis.Horizontal
                ? _horizontalOffsets
                : _verticalOffsets;
            int track = IndexOfOffset(offsets, offset);
            if (track >= 0)
            {
                coordinates.Add(new PanelCladdingSegmentCoordinate(ToTopologyAxis(axis), track, bay));
            }
        }
        return coordinates
            .Distinct()
            .OrderBy(item => item.Axis)
            .ThenBy(item => item.Track)
            .ThenBy(item => item.Bay)
            .ToArray();
    }

    private static PanelCladdingTopologyAxis ToTopologyAxis(PanelExtrusionAxis axis) =>
        axis == PanelExtrusionAxis.Horizontal
            ? PanelCladdingTopologyAxis.Horizontal
            : PanelCladdingTopologyAxis.Vertical;

    private static PanelExtrusionAxis FromTopologyAxis(PanelCladdingTopologyAxis axis) =>
        axis == PanelCladdingTopologyAxis.Horizontal
            ? PanelExtrusionAxis.Horizontal
            : PanelExtrusionAxis.Vertical;

    private void PopulatePanelMetadata()
    {
        if (_layout is null)
        {
            return;
        }
        string unit = PanelCladdingGridCanvas.UnitLabel(_layout.ModelUnitScaleToMillimeters);
        string panelId = string.IsNullOrWhiteSpace(_layout.ObjectName)
            ? $"PID-{_layout.ObjectId.ToString("N")[..8].ToUpperInvariant()}"
            : _layout.ObjectName;
        PanelIdText.Text = panelId;
        PanelIdText.ToolTip = _layout.ObjectId.ToString();
        WallTypeText.Text = FormatWallType(_layout.SystemCode);
        UnitsText.Text = unit;
        OverallSizeText.Text = $"{_layout.Width:0.0} x {_layout.Height:0.0} {unit}";
        DividerOffsetList.ItemsSource = BuildOffsetDisplay(unit);
        UpdateWorkbookStatus();
    }

    private IReadOnlyList<PanelOffsetDisplay> BuildOffsetDisplay(string unit)
    {
        var items = new List<PanelOffsetDisplay>(_horizontalOffsets.Count + _verticalOffsets.Count);
        items.AddRange(_horizontalOffsets.Select((value, index) => new PanelOffsetDisplay(
            $"H{index}", $"{value:0.00000}", PanelDimensionAxis.Row, index, value, unit)));
        items.AddRange(_verticalOffsets.Select((value, index) => new PanelOffsetDisplay(
            $"V{index}", $"{value:0.00000}", PanelDimensionAxis.Column, index, value, unit)));
        return items.Count == 0
            ? [new PanelOffsetDisplay("-", "—", PanelDimensionAxis.Row, -1, 0d, unit)]
            : items;
    }

    private void OnDividerOffsetGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is PanelOffsetDisplay { Index: >= 0 })
        {
            textBox.SelectAll();
        }
    }

    private void OnDividerOffsetPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not PanelOffsetDisplay offset)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            CommitDividerOffset(textBox, offset);
            PanelGridCanvas.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            textBox.Text = offset.Display;
            PanelGridCanvas.Focus();
            e.Handled = true;
        }
    }

    private void OnDividerOffsetLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_committingDividerOffset && sender is TextBox textBox && textBox.Tag is PanelOffsetDisplay offset)
        {
            CommitDividerOffset(textBox, offset);
        }
    }

    private void CommitDividerOffset(TextBox textBox, PanelOffsetDisplay offset)
    {
        if (_committingDividerOffset || offset.Index < 0)
        {
            return;
        }
        _committingDividerOffset = true;
        try
        {
            string candidate = textBox.Text.Trim();
            if (candidate.EndsWith(offset.Unit, StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[..^offset.Unit.Length].Trim();
            }
            bool parsed = double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || double.TryParse(candidate, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
            if (!parsed || !double.IsFinite(value) || !UpdateDividerOffset(offset.Axis, offset.Index, value))
            {
                textBox.Text = offset.Display;
            }
        }
        finally
        {
            _committingDividerOffset = false;
        }
    }

    private bool UpdateDividerOffset(PanelDimensionAxis axis, int index, double value)
    {
        if (_layout is null)
        {
            return false;
        }
        List<double> offsets = axis == PanelDimensionAxis.Column ? _verticalOffsets : _horizontalOffsets;
        double total = axis == PanelDimensionAxis.Column ? _layout.Width : _layout.Height;
        if (index < 0 || index >= offsets.Count)
        {
            return false;
        }
        double minimum = Math.Min(4d, total / Math.Max(2d, (offsets.Count + 1) * 2d));
        double lower = index == 0 ? minimum : offsets[index - 1] + minimum;
        double upper = index == offsets.Count - 1 ? total - minimum : offsets[index + 1] - minimum;
        if (value < lower || value > upper)
        {
            ShowToast($"Enter an offset from {lower:0.#####} to {upper:0.#####}");
            return false;
        }
        double rounded = Math.Round(value, 5, MidpointRounding.AwayFromZero);
        if (Math.Abs(offsets[index] - rounded) < 0.000005d)
        {
            return true;
        }
        PushUndoState();
        offsets[index] = rounded;
        _mergedGroups.Clear();
        _deletedExtrusions.Clear();
        _hiddenExtrusions.Clear();
        _selectedExtrusions.Clear();
        _layout = BuildWorkingLayout();
        MarkStructuralDirty();
        RefreshCanvas();
        if (_activeView == PanelEditorView.Extrusion)
        {
            UpdateExtrusionSelectionUi();
        }
        ShowToast($"{(axis == PanelDimensionAxis.Column ? "Vertical" : "Horizontal")} divider offset updated");
        return true;
    }

    private static string FormatWallType(string? systemCode)
    {
        string value = systemCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }
        int split = value.TakeWhile(char.IsLetter).Count();
        return split > 0 && split < value.Length && value[split] != '-'
            ? $"{value[..split]}-{value[split..]}"
            : value;
    }

    private void EnsureMaterialLibrary()
    {
        if (_layout is null)
        {
            return;
        }
        var references = _layout.Cells.Select(cell => cell.ShortLabel).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] codes = _values.Values
            .Where(value => !string.IsNullOrWhiteSpace(value) && !references.Contains(value.Trim()))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = _materials.ToDictionary(item => item.Code, item => item, StringComparer.OrdinalIgnoreCase);
        foreach (string code in codes)
        {
            if (!existing.ContainsKey(code))
            {
                existing[code] = PanelCladdingMaterialPalette.FromCode(code);
            }
        }
        _materials.Clear();
        foreach (PanelCladdingMaterial material in existing.Values.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
        {
            _materials.Add(material);
        }
    }

    private void LoadProjectMaterials()
    {
        _materials.Clear();
        if (string.IsNullOrWhiteSpace(_workbookPath) || !File.Exists(_workbookPath))
        {
            return;
        }
        OperationResponse<PanelCladdingMaterialCatalog> response = _controller.LoadMaterialCatalog(_workbookPath);
        if (!response.Success || response.Data is null)
        {
            return;
        }
        foreach (PanelCladdingMaterialCatalogItem item in response.Data.Materials)
        {
            try
            {
                _materials.Add(PanelCladdingMaterial.FromCatalogItem(item));
            }
            catch (Exception exception) when (exception is FormatException or NotSupportedException)
            {
                // Keep loading valid catalogue entries; malformed rows remain editable in Excel.
            }
        }
    }

    private void LoadProjectExtrusions()
    {
        _frameExtrusions.Clear();
        if (string.IsNullOrWhiteSpace(_workbookPath) || !File.Exists(_workbookPath))
        {
            RefreshAvailableExtrusions();
            return;
        }
        OperationResponse<PanelFrameExtrusionCatalog> response =
            _controller.LoadFrameExtrusionCatalog(_workbookPath);
        if (response.Success && response.Data is not null)
        {
            foreach (PanelFrameExtrusionCatalogItem item in response.Data.Extrusions)
            {
                if (item.Dimension is null)
                {
                    continue;
                }
                try
                {
                    _frameExtrusions.Add(PanelFrameExtrusion.FromCatalogItem(item));
                }
                catch (Exception exception) when (exception is IOException or NotSupportedException)
                {
                    // Keep valid project profiles available when one workbook thumbnail is malformed.
                }
            }
        }
        RefreshAvailableExtrusions();
    }

    private void EnsureFrameExtrusionLibrary()
    {
        HashSet<string> existing = _frameExtrusions
            .Select(item => item.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string code in _frameAssignmentCodes.Values
                     .SelectMany(values => values)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (existing.Add(code))
            {
                _frameExtrusions.Add(new PanelFrameExtrusion
                {
                    Code = code,
                    BaseCode = code.Length > 3 ? code[3..] : code,
                    SourceCode = code.Length > 3 ? code[3..] : code,
                    Description = "Assigned profile not found in the project catalogue",
                    Dimension = code.StartsWith("0D-", StringComparison.OrdinalIgnoreCase)
                        ? PanelFrameProfileDimension.ZeroDimensional
                        : PanelFrameProfileDimension.OneDimensional,
                    Calculation = _frameDefinitions.TryGetValue(code, out PanelFrameProfileDefinition? definition)
                        ? definition.Calculation
                        : PanelFrameProfileCalculation.Length,
                    CalculationValue = definition?.CalculationValue ?? 1d,
                    ParentCode = definition?.ParentCode ?? string.Empty
                });
            }
        }
        RefreshAvailableExtrusions();
    }

    private void RefreshAvailableExtrusions(IEnumerable<string>? excludedCodes = null)
    {
        HashSet<string> excluded = excludedCodes?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? CurrentSegments()
                .Where(segment => _selectedExtrusions.Contains(segment.Id))
                .SelectMany(AssignedCodes)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _availableFrameExtrusions.Clear();
        foreach (PanelFrameExtrusion extrusion in _frameExtrusions
                     .Where(item => item.IsConfigured && !excluded.Contains(item.Code))
                     .OrderBy(item => item.SourcePageNumber)
                     .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
        {
            _availableFrameExtrusions.Add(extrusion);
        }
        UpdateExtrusionCatalogueEmptyState();
    }

    private void UpdateExtrusionCatalogueEmptyState()
    {
        if (ExtrusionCatalogueEmptyText is not null)
        {
            bool hasReadyProfiles = _frameExtrusions.Any(item => item.IsConfigured);
            ExtrusionCatalogueEmptyText.Text = !hasReadyProfiles
                ? "Use Setup to import and configure the project extrusion schedule."
                : "All ready profiles are already assigned to the selected curves.";
            ExtrusionCatalogueEmptyText.Visibility = _availableFrameExtrusions.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private IReadOnlyDictionary<string, PanelCladdingMaterial> MaterialMap() =>
        _materials.ToDictionary(item => item.Code, item => item, StringComparer.OrdinalIgnoreCase);

    private void RefreshCanvas()
    {
        if (_layout is null)
        {
            return;
        }
        NormalizeCellSelection(CurrentCellTopology());
        PanelGridCanvas.SetLayout(_layout, _values, MaterialMap(), _selectedKeys);
        PanelGridCanvas.SetDimensionLocks(_columnLocks, _rowLocks);
        PanelGridCanvas.SetExtrusionState(
            _activeView,
            _selectedExtrusions,
            _mergedGroups,
            _deletedExtrusions,
            _hiddenExtrusions);
        PanelGridCanvas.SetExtrusionAssignments(VisibleAssignmentMap());
        DividerOffsetList.ItemsSource = BuildOffsetDisplay(PanelCladdingGridCanvas.UnitLabel(_layout.ModelUnitScaleToMillimeters));
        UpdateTypePreview();
        UpdateFrameTypologyPreview();
    }

    private void OnCanvasSelectionChanged(object? sender, IReadOnlyCollection<string> keys)
    {
        _selectedKeys.Clear();
        _selectedKeys.UnionWith(keys);
        UpdateSelectionUi(syncAssignmentMode: true);
    }

    private void OnCanvasExtrusionSelectionChanged(object? sender, IReadOnlyCollection<string> ids)
    {
        _selectedExtrusions.Clear();
        _selectedExtrusions.UnionWith(ids);
        UpdateExtrusionSelectionUi();
    }

    private void OnCanvasMaterialDropped(object? sender, PanelMaterialDropEventArgs e)
    {
        _selectedKeys.Clear();
        _selectedKeys.Add(e.CellKey);
        PanelGridCanvas.SetSelection(_selectedKeys);
        AssignSelected(e.MaterialCode, $"{e.MaterialCode} assigned");
    }

    private void OnExtrusionAssignmentDragOver(object sender, DragEventArgs e)
    {
        string? code = e.Data.GetData(FrameExtrusionDragFormat) as string;
        bool hasSelection = CurrentSegments().Any(segment => _selectedExtrusions.Contains(segment.Id));
        e.Effects = !string.IsNullOrWhiteSpace(code) && hasSelection
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnExtrusionAssignmentDrop(object sender, DragEventArgs e)
    {
        string? code = e.Data.GetData(FrameExtrusionDragFormat) as string;
        if (!string.IsNullOrWhiteSpace(code) &&
            CurrentSegments().Any(segment => _selectedExtrusions.Contains(segment.Id)))
        {
            AssignExtrusionCode(code);
        }
        e.Handled = true;
    }

    private void AssignExtrusionCode(string rawCode)
    {
        string code = rawCode.Trim().ToUpperInvariant();
        PanelFrameExtrusion? profile = _frameExtrusions.FirstOrDefault(item =>
            item.IsConfigured && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            ShowToast($"Configure {code} in Setup before assigning it");
            return;
        }
        OperationResponse<IReadOnlyList<PanelFrameExtrusion>> dependencyChain = ResolveDependencyChain(profile);
        if (!dependencyChain.Success || dependencyChain.Data is null)
        {
            ShowToast(dependencyChain.Message);
            return;
        }
        PanelExtrusionSegment[] selected = CurrentSegments()
            .Where(segment => _selectedExtrusions.Contains(segment.Id))
            .ToArray();
        string[] keys = selected
            .SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (keys.Length == 0)
        {
            ShowToast("Select one or more frame or intermediate curves first");
            return;
        }
        if (keys.All(key => _frameAssignmentCodes.TryGetValue(key, out HashSet<string>? codes) && codes.Contains(code)))
        {
            ShowToast($"{code} is already assigned");
            return;
        }
        PushUndoState();
        foreach (string key in keys)
        {
            if (!_frameAssignmentCodes.TryGetValue(key, out HashSet<string>? codes))
            {
                codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _frameAssignmentCodes[key] = codes;
            }
            if (codes.Contains(code))
            {
                continue;
            }
            foreach (PanelFrameExtrusion dependency in dependencyChain.Data)
            {
                codes.Add(dependency.Code);
                _frameDefinitions[dependency.Code] = dependency.ToDefinition();
            }
        }
        MarkExtrusionAssignmentDirty();
        EnsureFrameExtrusionLibrary();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast($"{code} assigned to {selected.Length} extrusion{(selected.Length == 1 ? string.Empty : "s")}");
    }

    private OperationResponse<IReadOnlyList<PanelFrameExtrusion>> ResolveDependencyChain(
        PanelFrameExtrusion profile)
    {
        var result = new List<PanelFrameExtrusion> { profile };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profile.Code };
        PanelFrameExtrusion current = profile;
        while (!string.IsNullOrWhiteSpace(current.ParentCode))
        {
            if (!visited.Add(current.ParentCode))
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusion>>.Fail(
                    $"Dependency cycle detected at {current.ParentCode}");
            }
            PanelFrameExtrusion? parent = _frameExtrusions.FirstOrDefault(item =>
                item.IsConfigured && string.Equals(item.Code, current.ParentCode, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
            {
                return OperationResponse<IReadOnlyList<PanelFrameExtrusion>>.Fail(
                    $"Parent profile {current.ParentCode} is not in the ready catalogue");
            }
            result.Add(parent);
            current = parent;
        }
        return OperationResponse<IReadOnlyList<PanelFrameExtrusion>>.Ok(result);
    }

    private void RemoveExtrusionCodeFromSelection(string rawCode)
    {
        string code = rawCode.Trim().ToUpperInvariant();
        string[] keys = CurrentSegments()
            .Where(segment => _selectedExtrusions.Contains(segment.Id))
            .SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!keys.Any(key => _frameAssignmentCodes.TryGetValue(key, out HashSet<string>? codes) && codes.Contains(code)))
        {
            return;
        }
        PushUndoState();
        foreach (string key in keys)
        {
            if (!_frameAssignmentCodes.TryGetValue(key, out HashSet<string>? codes))
            {
                continue;
            }
            codes.Remove(code);
            if (codes.Count == 0)
            {
                _frameAssignmentCodes.Remove(key);
                _frameCurveModifiers.Remove(key);
            }
        }
        MarkExtrusionAssignmentDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast($"{code} removed from the selected extrusions");
    }

    private void OnRemoveExtrusionAssignmentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PanelExtrusionAssignmentCard card })
        {
            RemoveExtrusionCodeFromSelection(card.Code);
        }
    }

    private void OnClearExtrusionAssignmentsClick(object sender, RoutedEventArgs e)
    {
        string[] codes = CurrentSegments()
            .Where(segment => _selectedExtrusions.Contains(segment.Id))
            .SelectMany(AssignedCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (codes.Length == 0)
        {
            return;
        }
        PushUndoState();
        string[] keys = CurrentSegments()
            .Where(segment => _selectedExtrusions.Contains(segment.Id))
            .SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string key in keys)
        {
            _frameAssignmentCodes.Remove(key);
            _frameCurveModifiers.Remove(key);
        }
        MarkExtrusionAssignmentDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast("Extrusion assignments cleared from the selection");
    }

    private void OnAssignedModifierPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }
        ApplyCurveModifier(textBox, force: true);
        e.Handled = true;
        Keyboard.ClearFocus();
    }

    private void OnAssignedModifierLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ApplyCurveModifier(textBox, force: false);
        }
    }

    private void ApplyCurveModifier(TextBox textBox, bool force)
    {
        if (!force && textBox.DataContext is PanelExtrusionAssignmentCard card &&
            string.Equals(textBox.Text.Trim(), card.ModifierText, StringComparison.Ordinal))
        {
            return;
        }
        string text = textBox.Text.Trim();
        double modifier = 0d;
        if (!string.IsNullOrWhiteSpace(text) &&
            (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out modifier) ||
             !double.IsFinite(modifier) || Math.Abs(modifier) > 100_000d))
        {
            ShowToast("Enter a number such as +4 or -5");
            textBox.Text = textBox.DataContext is PanelExtrusionAssignmentCard invalidCard
                ? invalidCard.ModifierText
                : string.Empty;
            return;
        }
        string[] keys = CurrentSegments()
            .Where(segment => _selectedExtrusions.Contains(segment.Id))
            .SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(_frameAssignmentCodes.ContainsKey)
            .ToArray();
        if (keys.Length == 0)
        {
            ShowToast("Assign at least one profile before applying a length modifier");
            return;
        }
        if (keys.All(key => Math.Abs(_frameCurveModifiers.GetValueOrDefault(key) - modifier) < 1e-9d))
        {
            return;
        }
        PushUndoState();
        foreach (string key in keys)
        {
            if (Math.Abs(modifier) < 1e-9d)
            {
                _frameCurveModifiers.Remove(key);
            }
            else
            {
                _frameCurveModifiers[key] = modifier;
            }
        }
        MarkExtrusionAssignmentDirty();
        UpdateExtrusionSelectionUi();
        ShowToast(Math.Abs(modifier) < 1e-9d
            ? "Length modifier cleared"
            : $"Length modifier {FormatSigned(modifier)} applied");
    }

    private void UpdateSelectionUi(bool syncAssignmentMode)
    {
        int count = _selectedKeys.Count;
        SelectedCountText.Text = count == 1 ? "1 cell" : $"{count} cells";
        ClearCellsButton.IsEnabled = count > 0;
        MaterialSelect.IsEnabled = count > 0;
        ParentReferenceSelect.IsEnabled = count > 0;
        PanelCladdingCell[] availableParents = _layout is null
            ? Array.Empty<PanelCladdingCell>()
            : OrderParentReferenceCells(CurrentCellTopology().Groups
                .Select(group => group.Representative)
                .Where(cell => !_selectedKeys.Contains(cell.UserTextKey)));

        _syncingAssignmentControls = true;
        try
        {
            ParentReferenceSelect.ItemsSource = availableParents;
            if (!syncAssignmentMode || count == 0)
            {
                MaterialSelect.SelectedItem = null;
                ParentReferenceSelect.SelectedItem = null;
                return;
            }
            string[] assignments = SelectedCells()
                .Select(cell => _values.GetValueOrDefault(cell.UserTextKey, string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (assignments.Length == 1 && TryGetCellByLabel(assignments[0], out _))
            {
                SwitchAssignmentMode(parentMode: true);
                ParentReferenceSelect.SelectedItem = availableParents.FirstOrDefault(cell =>
                    string.Equals(cell.ShortLabel, assignments[0], StringComparison.OrdinalIgnoreCase));
                MaterialSelect.SelectedItem = null;
            }
            else
            {
                SwitchAssignmentMode(parentMode: false);
                MaterialSelect.SelectedItem = assignments.Length == 1
                    ? _materials.FirstOrDefault(item => string.Equals(item.Code, assignments[0], StringComparison.OrdinalIgnoreCase))
                    : null;
                ParentReferenceSelect.SelectedItem = null;
            }
        }
        finally
        {
            _syncingAssignmentControls = false;
        }
    }

    private void UpdateExtrusionSelectionUi()
    {
        PanelExtrusionSegment[] selected = CurrentSegments()
            .Where(item => _selectedExtrusions.Contains(item.Id))
            .ToArray();
        int count = selected.Length;
        SelectedExtrusionCountText.Text = count == 1 ? "1 extrusion" : $"{count} extrusions";
        MergeExtrusionsButton.IsEnabled = count >= 2;
        ExplodeExtrusionsButton.IsEnabled = selected.Any(item => item.IsMerged);
        string[] editableAtoms = selected
            .Where(item => !item.IsFrame)
            .SelectMany(item => item.AtomicIds.Count > 0 ? item.AtomicIds : [item.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        bool allHidden = editableAtoms.Length > 0 && editableAtoms.All(_hiddenExtrusions.Contains);
        HideExtrusionsButton.Content = allHidden ? "Unhide" : "Hide";
        HideExtrusionsButton.IsEnabled = editableAtoms.Length > 0;
        MergeExtrusionsButton.IsEnabled = count >= 2 && !editableAtoms.Any(_hiddenExtrusions.Contains);
        DeleteExtrusionsButton.IsEnabled = selected.Any(item => !item.IsFrame);
        string[] assignedCodes = selected
            .SelectMany(AssignedCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        ClearExtrusionAssignmentsButton.IsEnabled = count > 0 && assignedCodes.Length > 0;
        string[] modifierKeys = selected.SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(_frameAssignmentCodes.ContainsKey)
            .ToArray();
        double[] modifiers = modifierKeys
            .Select(key => _frameCurveModifiers.GetValueOrDefault(key))
            .Distinct()
            .ToArray();
        string modifierText = modifiers.Length == 1 && Math.Abs(modifiers[0]) > 1e-9d
            ? FormatSigned(modifiers[0])
            : string.Empty;
        string modifierToolTip = modifiers.Length > 1
            ? "The selected curves have mixed length modifiers. Enter a value to replace them."
            : "Optional curve-length adjustment, such as +4 or -5.";
        SelectedExtrusionAssignments.ItemsSource = assignedCodes
            .Select(code =>
            {
                PanelFrameExtrusion? profile = _frameExtrusions.FirstOrDefault(item =>
                    string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
                return new PanelExtrusionAssignmentCard(
                    code,
                    profile?.DisplayName ?? code,
                    profile?.Thumbnail,
                    modifierText,
                    modifierToolTip);
            })
            .ToArray();
        RefreshAvailableExtrusions(assignedCodes);
        ExtrusionAssignmentHelpText.Text = count == 0
            ? "Select a frame or intermediate curve, then drag a profile here."
            : assignedCodes.Length == 0
                ? "Drop an extrusion profile here to assign it to the selected curves."
                : "Modifiers apply to curve length. Remove a card to make that profile available again.";
        PanelGridCanvas.SetExtrusionState(
            _activeView,
            _selectedExtrusions,
            _mergedGroups,
            _deletedExtrusions,
            _hiddenExtrusions);
        PanelGridCanvas.SetExtrusionAssignments(VisibleAssignmentMap());
        UpdateFrameTypologyPreview();
    }

    private static string FormatSigned(double value) =>
        $"{(value > 0d ? "+" : string.Empty)}{value.ToString("0.#####", CultureInfo.InvariantCulture)}";

    private IEnumerable<PanelCladdingCell> SelectedCells()
    {
        if (_layout is null)
        {
            return [];
        }
        return CurrentCellTopology().Groups
            .Where(group => _selectedKeys.Contains(group.Representative.UserTextKey))
            .SelectMany(group => group.Cells);
    }

    private void OnMaterialTabClick(object sender, RoutedEventArgs e) => SwitchAssignmentMode(parentMode: false);

    private void OnParentTabClick(object sender, RoutedEventArgs e) => SwitchAssignmentMode(parentMode: true);

    private void SwitchAssignmentMode(bool parentMode)
    {
        ParentAssignmentPanel.Visibility = parentMode ? Visibility.Visible : Visibility.Collapsed;
        MaterialAssignmentPanel.Visibility = parentMode ? Visibility.Collapsed : Visibility.Visible;
        ParentTabButton.Tag = parentMode ? "Selected" : "Unselected";
        MaterialTabButton.Tag = parentMode ? "Unselected" : "Selected";
    }

    private void OnMaterialSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingAssignmentControls || _isLoading || _selectedKeys.Count == 0 ||
            MaterialSelect.SelectedItem is not PanelCladdingMaterial material)
        {
            return;
        }
        AssignSelected(material.Code, $"{material.Code} preview assigned");
    }

    private void OnParentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingAssignmentControls || _isLoading || _selectedKeys.Count == 0 ||
            ParentReferenceSelect.SelectedItem is not PanelCladdingCell parent ||
            _selectedKeys.Contains(parent.UserTextKey))
        {
            return;
        }
        AssignSelected(parent.ShortLabel, $"Linked preview to parent cell {parent.ShortLabel}");
    }

    private bool TryGetCellByLabel(string? label, out PanelCladdingCell? cell)
    {
        cell = _layout is null
            ? null
            : CurrentCellTopology().FindByLabel(label?.Trim())?.Representative;
        return cell is not null;
    }

    private void OnClearCellsClick(object sender, RoutedEventArgs e) => AssignSelected(string.Empty, "Assignment cleared");

    private void AssignSelected(string value, string toast)
    {
        PanelCladdingCell[] targets = SelectedCells().ToArray();
        if (targets.Length == 0 || !targets.Any(cell =>
            !string.Equals(_values.GetValueOrDefault(cell.UserTextKey), value, StringComparison.Ordinal)))
        {
            return;
        }
        PushUndoState();
        foreach (PanelCladdingCell cell in targets)
        {
            _values[cell.UserTextKey] = value;
        }
        MarkCladdingDirty();
        PanelGridCanvas.SetValues(_values, MaterialMap());
        UpdateSelectionUi(syncAssignmentMode: true);
        UpdateTypePreview();
        ShowToast(toast);
    }

    private void PushUndoState()
    {
        _undoStack.Push(CaptureSnapshot());
        if (_undoStack.Count <= 50)
        {
            return;
        }
        EditorSnapshot[] kept = _undoStack.Take(50).Reverse().ToArray();
        _undoStack.Clear();
        foreach (EditorSnapshot state in kept)
        {
            _undoStack.Push(state);
        }
    }

    private EditorSnapshot CaptureSnapshot() => new(
        new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase),
        _frameAssignmentCodes.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OrderBy(code => code, StringComparer.Ordinal).ToArray(),
            StringComparer.OrdinalIgnoreCase),
        _frameDefinitions.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        _frameCurveModifiers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        _horizontalOffsets.ToArray(),
        _verticalOffsets.ToArray(),
        _rowLocks.ToArray(),
        _columnLocks.ToArray(),
        _mergedGroups.Select(group => group with { AtomicIds = group.AtomicIds.ToArray() }).ToArray(),
        _deletedExtrusions.ToArray(),
        _hiddenExtrusions.ToArray(),
        _selectedKeys.ToArray(),
        _selectedExtrusions.ToArray(),
        _structuralDirty,
        _claddingDirty);

    private void UndoLastChange()
    {
        if (_undoStack.Count == 0)
        {
            ShowToast("Nothing to undo");
            return;
        }
        RestoreSnapshot(_undoStack.Pop());
        RefreshDirtyState();
        PopulatePanelMetadata();
        RefreshCanvas();
        if (_activeView == PanelEditorView.Extrusion)
        {
            UpdateExtrusionSelectionUi();
        }
        else
        {
            UpdateSelectionUi(syncAssignmentMode: true);
        }
        ShowToast("Last edit restored");
    }

    private void RestoreSnapshot(EditorSnapshot state)
    {
        _values.Clear();
        foreach ((string key, string value) in state.Values)
        {
            _values[key] = value;
        }
        _frameAssignmentCodes.Clear();
        foreach ((string key, IReadOnlyList<string> codes) in state.FrameAssignments)
        {
            _frameAssignmentCodes[key] = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        _frameDefinitions.Clear();
        foreach ((string code, PanelFrameProfileDefinition definition) in state.FrameDefinitions)
        {
            _frameDefinitions[code] = definition;
        }
        _frameCurveModifiers.Clear();
        foreach ((string key, double modifier) in state.FrameModifiers)
        {
            _frameCurveModifiers[key] = modifier;
        }
        Replace(_horizontalOffsets, state.HorizontalOffsets);
        Replace(_verticalOffsets, state.VerticalOffsets);
        Replace(_rowLocks, state.RowLocks);
        Replace(_columnLocks, state.ColumnLocks);
        _mergedGroups.Clear();
        _mergedGroups.AddRange(state.MergedGroups);
        _deletedExtrusions.Clear();
        _deletedExtrusions.UnionWith(state.DeletedExtrusions);
        _hiddenExtrusions.Clear();
        _hiddenExtrusions.UnionWith(state.HiddenExtrusions);
        _selectedKeys.Clear();
        _selectedKeys.UnionWith(state.SelectedKeys);
        _selectedExtrusions.Clear();
        _selectedExtrusions.UnionWith(state.SelectedExtrusions);
        _structuralDirty = state.StructuralDirty;
        _claddingDirty = state.CladdingDirty;
        _layout = BuildWorkingLayout();
        EnsureMaterialLibrary();
    }

    private static void Replace<T>(List<T> target, IEnumerable<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }

    private void OnCladdingViewClick(object sender, RoutedEventArgs e) => SwitchView(PanelEditorView.Cladding, showNotice: false);

    private void OnExtrusionViewClick(object sender, RoutedEventArgs e) => SwitchView(PanelEditorView.Extrusion, showNotice: true);

    private void SwitchView(PanelEditorView view, bool showNotice)
    {
        _activeView = view;
        bool extrusion = view == PanelEditorView.Extrusion;
        if (!extrusion)
        {
            SetMullionPlacement(null);
        }
        ExtrusionViewButton.Tag = extrusion ? "Selected" : "Unselected";
        CladdingViewButton.Tag = extrusion ? "Unselected" : "Selected";
        ClearCellsButton.Visibility = extrusion ? Visibility.Collapsed : Visibility.Visible;
        MaterialLegendSection.Visibility = extrusion ? Visibility.Collapsed : Visibility.Visible;
        ExtrusionLegendSection.Visibility = extrusion ? Visibility.Visible : Visibility.Collapsed;
        CladdingAssignmentSection.Visibility = extrusion ? Visibility.Collapsed : Visibility.Visible;
        ExtrusionAssignmentSection.Visibility = extrusion ? Visibility.Visible : Visibility.Collapsed;
        CladdingTypeSection.Visibility = extrusion ? Visibility.Collapsed : Visibility.Visible;
        FrameTypologySection.Visibility = extrusion ? Visibility.Visible : Visibility.Collapsed;
        ExtrusionActions.Visibility = extrusion ? Visibility.Visible : Visibility.Collapsed;
        PanelGridCanvas.SetExtrusionState(
            view,
            _selectedExtrusions,
            _mergedGroups,
            _deletedExtrusions,
            _hiddenExtrusions);
        if (extrusion)
        {
            UpdateExtrusionSelectionUi();
            if (showNotice)
            {
                ShowToast("Extrusion layout edits are reversible session previews");
            }
        }
        else
        {
            UpdateSelectionUi(syncAssignmentMode: true);
        }
    }

    private IReadOnlyList<PanelExtrusionSegment> CurrentSegments()
    {
        if (_layout is null)
        {
            return Array.Empty<PanelExtrusionSegment>();
        }
        return PanelExtrusionTopology.Build(
            _layout.Width,
            _layout.Height,
            _horizontalOffsets,
            _verticalOffsets,
            _mergedGroups,
            _deletedExtrusions);
    }

    private PanelCladdingCellTopology CurrentCellTopology() =>
        PanelCladdingCellTopology.Build(
            _layout ?? throw new InvalidOperationException("A working layout is required."),
            _deletedExtrusions);

    internal static PanelCladdingCell[] OrderParentReferenceCells(
        IEnumerable<PanelCladdingCell> cells) => cells
            .OrderBy(cell => cell.Column)
            .ThenBy(cell => cell.Row)
            .ThenBy(cell => cell.ShortLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void NormalizeCellSelection(PanelCladdingCellTopology topology)
    {
        string[] representatives = _selectedKeys
            .Select(key => topology.FindByCellKey(key)?.Representative.UserTextKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _selectedKeys.Clear();
        _selectedKeys.UnionWith(representatives);
    }

    private void NormalizeParentReferences(PanelCladdingCellTopology topology)
    {
        foreach (string key in _values.Keys.ToArray())
        {
            string value = _values.GetValueOrDefault(key, string.Empty).Trim();
            if (!PanelCladdingKeyService.IsCellLabelToken(value))
            {
                continue;
            }
            PanelCladdingCellGroup? target = topology.FindByLabel(value);
            if (target is not null)
            {
                _values[key] = target.Representative.ShortLabel;
            }
        }
    }

    private void OnMergeExtrusionsClick(object sender, RoutedEventArgs e)
    {
        PanelExtrusionSegment[] segments = CurrentSegments()
            .Where(item => _selectedExtrusions.Contains(item.Id))
            .OrderBy(item => item.Start)
            .ToArray();
        if (segments.Length < 2)
        {
            ShowToast("Select at least two intermediate segments to merge");
            return;
        }
        if (segments.Any(item => item.IsFrame))
        {
            ShowToast("Outer frame extrusions are already continuous");
            return;
        }
        if (segments.Select(item => item.Axis).Distinct().Count() != 1 ||
            segments.Select(item => Math.Round(item.Offset, 5)).Distinct().Count() != 1)
        {
            ShowToast("Selected segments must lie on one straight mullion");
            return;
        }
        if (segments.Skip(1).Where((item, index) => Math.Abs(item.Start - segments[index].End) > 0.05d).Any())
        {
            ShowToast("Selected segments must form one adjacent run");
            return;
        }
        string[] atomicIds = segments.SelectMany(item => item.AtomicIds.Count > 0 ? item.AtomicIds : [item.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] assignmentKeys = segments.SelectMany(AssignmentKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        double[] selectedModifiers = assignmentKeys
            .Select(key => _frameCurveModifiers.GetValueOrDefault(key))
            .Distinct()
            .ToArray();
        if (selectedModifiers.Length > 1)
        {
            ShowToast("Selected segments must use the same length modifier before merging");
            return;
        }
        PushUndoState();
        string[] mergedCodes = assignmentKeys
            .SelectMany(key => _frameAssignmentCodes.TryGetValue(key, out HashSet<string>? codes)
                ? codes
                : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string key in assignmentKeys)
        {
            if (mergedCodes.Length == 0)
            {
                _frameAssignmentCodes.Remove(key);
            }
            else
            {
                _frameAssignmentCodes[key] = mergedCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            if (selectedModifiers.Length == 1 && Math.Abs(selectedModifiers[0]) > 1e-9d)
            {
                _frameCurveModifiers[key] = selectedModifiers[0];
            }
            else
            {
                _frameCurveModifiers.Remove(key);
            }
        }
        var selectedMerged = segments.Where(item => item.IsMerged).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _mergedGroups.RemoveAll(group => selectedMerged.Contains(group.Id));
        string id = $"M:{segments[0].Axis}:{segments[0].Offset.ToString("0.#####", CultureInfo.InvariantCulture)}:{string.Join('+', atomicIds)}";
        _mergedGroups.Add(new PanelExtrusionMergeGroup(id, segments[0].Axis, segments[0].Offset, atomicIds));
        _selectedExtrusions.Clear();
        _selectedExtrusions.Add(id);
        MarkStructuralDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast($"{atomicIds.Length} adjacent segments merged");
    }

    private void OnExplodeExtrusionsClick(object sender, RoutedEventArgs e)
    {
        HashSet<string> selectedMerged = CurrentSegments()
            .Where(item => item.IsMerged && _selectedExtrusions.Contains(item.Id))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selectedMerged.Count == 0)
        {
            ShowToast("Select a merged intermediate extrusion to explode");
            return;
        }
        PushUndoState();
        _mergedGroups.RemoveAll(group => selectedMerged.Contains(group.Id));
        _selectedExtrusions.Clear();
        MarkStructuralDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast("Selected extrusions exploded at their intersections");
    }

    private void OnDeleteExtrusionsClick(object sender, RoutedEventArgs e)
    {
        PanelExtrusionSegment[] editable = CurrentSegments()
            .Where(item => !item.IsFrame && _selectedExtrusions.Contains(item.Id))
            .ToArray();
        if (editable.Length == 0)
        {
            ShowToast("Select one or more intermediate segments to delete");
            return;
        }
        PushUndoState();
        HashSet<string> mergedIds = editable.Where(item => item.IsMerged).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _mergedGroups.RemoveAll(group => mergedIds.Contains(group.Id));
        string[] atomicIds = editable.SelectMany(item => item.AtomicIds.Count > 0 ? item.AtomicIds : [item.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _hiddenExtrusions.ExceptWith(atomicIds);
        _deletedExtrusions.UnionWith(atomicIds);
        RemoveFrameAssignments(atomicIds);
        ResetAssignmentsForTopologyChange(atomicIds);
        int collapsedTracks = NormalizeFullyDeletedTracks();
        _selectedExtrusions.Clear();
        MarkStructuralDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast(collapsedTracks > 0
            ? $"{collapsedTracks} empty extrusion {(collapsedTracks == 1 ? "track" : "tracks")} removed · cells renumbered"
            : $"{editable.Length} extrusion {(editable.Length == 1 ? "segment" : "segments")} deleted");
    }

    private void OnHideExtrusionsClick(object sender, RoutedEventArgs e)
    {
        PanelExtrusionSegment[] editable = CurrentSegments()
            .Where(item => !item.IsFrame && _selectedExtrusions.Contains(item.Id))
            .ToArray();
        string[] atomicIds = editable
            .SelectMany(item => item.AtomicIds.Count > 0 ? item.AtomicIds : [item.Id])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (atomicIds.Length == 0)
        {
            ShowToast("Select one or more intermediate segments to hide");
            return;
        }

        PushUndoState();
        bool unhide = atomicIds.All(_hiddenExtrusions.Contains);
        string[] selectedIds = editable.Select(item => item.Id).ToArray();
        if (unhide)
        {
            _hiddenExtrusions.ExceptWith(atomicIds);
        }
        else
        {
            _hiddenExtrusions.UnionWith(atomicIds);
        }
        _selectedExtrusions.Clear();
        _selectedExtrusions.UnionWith(selectedIds);
        MarkStructuralDirty();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast(unhide
            ? $"{atomicIds.Length} extrusion {(atomicIds.Length == 1 ? "segment" : "segments")} restored"
            : $"{atomicIds.Length} extrusion {(atomicIds.Length == 1 ? "segment" : "segments")} hidden · cladding cells retained");
    }

    private int NormalizeFullyDeletedTracks()
    {
        if (_layout is null)
        {
            return 0;
        }
        OperationResponse<PanelCladdingTopologyNormalizationResult> response =
            _topologyNormalizer.Normalize(
                _horizontalOffsets,
                _verticalOffsets,
                _values,
                CaptureTopologyState(),
                _layout.Width,
                _layout.Height,
                _layout.ModelTolerance);
        if (!response.Success || response.Data is null)
        {
            throw new InvalidOperationException(response.Message);
        }
        PanelCladdingTopologyNormalizationResult normalized = response.Data;
        if (normalized.CollapsedTracks.Count == 0)
        {
            return 0;
        }

        foreach (PanelCladdingCollapsedTrack collapsed in normalized.CollapsedTracks)
        {
            RemapFrameAssignmentsForCollapsedTrack(collapsed);
            List<bool> locks = collapsed.Axis == PanelCladdingTopologyAxis.Horizontal
                ? _rowLocks
                : _columnLocks;
            if (collapsed.Track >= 0 && collapsed.Track + 1 < locks.Count)
            {
                locks[collapsed.Track] = locks[collapsed.Track] || locks[collapsed.Track + 1];
                locks.RemoveAt(collapsed.Track + 1);
            }
        }
        Replace(_horizontalOffsets, normalized.HorizontalOffsets);
        Replace(_verticalOffsets, normalized.VerticalOffsets);
        _values.Clear();
        foreach ((string key, string value) in normalized.CellValues)
        {
            _values[key] = value;
        }
        _deletedExtrusions.Clear();
        _hiddenExtrusions.Clear();
        _mergedGroups.Clear();
        _selectedExtrusions.Clear();
        _selectedKeys.Clear();
        _layout = BuildWorkingLayout();
        ApplyTopologyState(normalized.Topology);
        _layout = BuildWorkingLayout();
        PopulatePanelMetadata();
        return normalized.CollapsedTracks.Count;
    }

    private void ResetAssignmentsForTopologyChange(IEnumerable<string> atomicIds)
    {
        if (_layout is null)
        {
            return;
        }
        var affectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in atomicIds)
        {
            if (!PanelExtrusionTopology.TryParseAtomicId(id, out PanelExtrusionAxis axis, out double offset, out int bay))
            {
                continue;
            }
            int line = IndexOfOffset(axis == PanelExtrusionAxis.Horizontal ? _horizontalOffsets : _verticalOffsets, offset);
            if (line < 0)
            {
                continue;
            }
            IEnumerable<(int Column, int Row)> positions = axis == PanelExtrusionAxis.Horizontal
                ? [(bay, line), (bay, line + 1)]
                : [(line, bay), (line + 1, bay)];
            foreach ((int column, int row) in positions)
            {
                PanelCladdingCell? cell = _layout.Cells.FirstOrDefault(item => item.Column == column && item.Row == row);
                if (cell is not null)
                {
                    affectedKeys.Add(cell.UserTextKey);
                }
            }
        }
        PanelCladdingCellTopology topology = CurrentCellTopology();
        foreach (PanelCladdingCellGroup group in affectedKeys
                     .Select(topology.FindByCellKey)
                     .OfType<PanelCladdingCellGroup>()
                     .Distinct())
        {
            foreach (PanelCladdingCell cell in group.Cells)
            {
                _values[cell.UserTextKey] = string.Empty;
            }
        }
        NormalizeParentReferences(topology);
        NormalizeCellSelection(topology);
    }

    private static int IndexOfOffset(IReadOnlyList<double> values, double target)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (Math.Abs(values[index] - target) < 1e-5d)
            {
                return index;
            }
        }
        return -1;
    }

    private void OnAddHorizontalMullionClick(object sender, RoutedEventArgs e) => ToggleMullionPlacement(PanelExtrusionAxis.Horizontal);

    private void OnAddVerticalMullionClick(object sender, RoutedEventArgs e) => ToggleMullionPlacement(PanelExtrusionAxis.Vertical);

    private void ToggleMullionPlacement(PanelExtrusionAxis axis)
    {
        SetMullionPlacement(_placementAxis == axis ? null : axis);
        if (_placementAxis is not null)
        {
            ShowToast($"Hover a cladding cell, then click to place a {_placementAxis.Value.ToString().ToLowerInvariant()} segment");
        }
    }

    private void SetMullionPlacement(PanelExtrusionAxis? axis)
    {
        _placementAxis = axis;
        AddHorizontalMullionButton.Tag = axis == PanelExtrusionAxis.Horizontal;
        AddVerticalMullionButton.Tag = axis == PanelExtrusionAxis.Vertical;
        PanelGridCanvas.SetMullionPlacement(axis);
    }

    private void OnMullionPlacementRequested(object? sender, PanelMullionPlacementEventArgs e)
    {
        PanelCladdingCellGroup? target = _layout is null
            ? null
            : CurrentCellTopology().FindByCellKey(e.CellKey);
        if (target is not null)
        {
            OpenMullionDialog(e.Axis, target);
        }
    }

    private void OpenMullionDialog(PanelExtrusionAxis axis, PanelCladdingCellGroup target)
    {
        if (_layout is null)
        {
            return;
        }
        bool horizontal = axis == PanelExtrusionAxis.Horizontal;
        double[] cuts = horizontal
            ? [0d, .. _horizontalOffsets, _layout.Height]
            : [0d, .. _verticalOffsets, _layout.Width];
        int firstBay = horizontal ? target.MinimumRow : target.MinimumColumn;
        int lastBay = horizontal ? target.MaximumRow : target.MaximumColumn;
        double origin = cuts[firstBay];
        double localTotal = cuts[lastBay + 1] - cuts[firstBay];
        double initial = localTotal / 2d;
        string prefix = horizontal ? "H" : "V";
        string direction = horizontal ? "horizontal" : "vertical";
        string targetLabel = target.Representative.ShortLabel;
        var dialog = new PanelNumericEditorDialog(
            $"Add {direction} segment in {targetLabel}",
            $"Set the orthogonal distance from the lower-left corner of cladding cell {targetLabel}.",
            $"{prefix} distance within cell {targetLabel}",
            initial,
            0d,
            localTotal,
            "Add mullion",
            exclusiveBounds: true)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            AddMullion(axis, origin + dialog.Value, target.Representative.UserTextKey);
            SetMullionPlacement(null);
        }
    }

    private static double LargestGapMidpoint(IReadOnlyList<double> values, double total)
    {
        double[] cuts = [0d, .. values.OrderBy(value => value), total];
        double size = -1d;
        double midpoint = total / 2d;
        for (int index = 0; index < cuts.Length - 1; index++)
        {
            double gap = cuts[index + 1] - cuts[index];
            if (gap > size)
            {
                size = gap;
                midpoint = cuts[index] + gap / 2d;
            }
        }
        return midpoint;
    }

    private void AddMullion(PanelExtrusionAxis axis, double offset) => AddMullion(axis, offset, null);

    private void AddMullion(PanelExtrusionAxis axis, double offset, string? targetCellKey)
    {
        if (_layout is null)
        {
            return;
        }
        List<double> offsets = axis == PanelExtrusionAxis.Horizontal ? _horizontalOffsets : _verticalOffsets;
        double total = axis == PanelExtrusionAxis.Horizontal ? _layout.Height : _layout.Width;
        offset = Math.Round(offset, 5, MidpointRounding.AwayFromZero);
        if (offset <= 0d || offset >= total || offsets.Any(value => Math.Abs(value - offset) < 1e-5d))
        {
            ShowToast("A valid new mullion offset is required");
            return;
        }
        if (axis == PanelExtrusionAxis.Horizontal && _horizontalOffsets.Count >= 25)
        {
            ShowToast("The cladding grid supports rows A through Z");
            return;
        }
        PanelCladdingCellGroup? targetGroup = targetCellKey is null
            ? null
            : CurrentCellTopology().FindByCellKey(targetCellKey);
        if (targetCellKey is not null && targetGroup is null)
        {
            ShowToast("Choose a valid cladding cell");
            return;
        }
        PushUndoState();
        int splitIndex = offsets.Count(value => value < offset);
        IReadOnlySet<string>? affectedCellKeys = targetGroup?.Cells
            .Select(cell => cell.UserTextKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlySet<int>? targetBays = targetGroup?.Cells
            .Select(cell => axis == PanelExtrusionAxis.Horizontal ? cell.Column : cell.Row)
            .ToHashSet();
        RemapCellsForInsertedMullion(axis, splitIndex, affectedCellKeys);
        RemapExtrusionTopologyForInsertedMullion(axis, splitIndex);
        offsets.Add(offset);
        offsets.Sort();
        SplitDimensionLock(axis, splitIndex);
        _layout = BuildWorkingLayout();
        _selectedKeys.Clear();
        _selectedExtrusions.Clear();
        PanelExtrusionSegment[] newLine = CurrentSegments()
            .Where(item => !item.IsFrame && item.Axis == axis && Math.Abs(item.Offset - offset) < 1e-5d)
            .ToArray();
        foreach (PanelExtrusionSegment segment in newLine)
        {
            bool withinTarget = targetBays is null || targetBays.Contains(SegmentBay(segment));
            if (withinTarget)
            {
                _selectedExtrusions.Add(segment.Id);
            }
            else
            {
                _deletedExtrusions.UnionWith(segment.AtomicIds);
            }
        }
        PanelCladdingCellTopology topology = CurrentCellTopology();
        NormalizeParentReferences(topology);
        NormalizeCellSelection(topology);
        MarkStructuralDirty();
        PopulatePanelMetadata();
        RefreshCanvas();
        UpdateExtrusionSelectionUi();
        ShowToast($"{(axis == PanelExtrusionAxis.Horizontal ? 'H' : 'V')}{splitIndex} added · new cladding cells are unassigned");
    }

    private static int SegmentBay(PanelExtrusionSegment segment) =>
        segment.AtomicIds.Count > 0 && PanelExtrusionTopology.TryParseAtomicId(segment.AtomicIds[0], out _, out _, out int bay)
            ? bay
            : -1;

    private void RemapCellsForInsertedMullion(
        PanelExtrusionAxis axis,
        int splitIndex,
        IReadOnlySet<string>? affectedCellKeys)
    {
        if (_layout is null)
        {
            return;
        }
        Dictionary<string, string> previous = new(_values, StringComparer.OrdinalIgnoreCase);
        PanelCladdingCell[] previousCells = _layout.Cells.ToArray();
        _values.Clear();
        foreach (PanelCladdingCell cell in previousCells)
        {
            int nextColumn = axis == PanelExtrusionAxis.Vertical && cell.Column > splitIndex ? cell.Column + 1 : cell.Column;
            int nextRow = axis == PanelExtrusionAxis.Horizontal && cell.Row > splitIndex ? cell.Row + 1 : cell.Row;
            bool inSplitBay = axis == PanelExtrusionAxis.Vertical ? cell.Column == splitIndex : cell.Row == splitIndex;
            bool affected = affectedCellKeys is null
                ? inSplitBay
                : affectedCellKeys.Contains(cell.UserTextKey);
            string nextRowLabel = PanelCladdingKeyService.GetRowLabel(nextRow);
            string nextKey = PanelCladdingKeyService.GetCellKey(nextColumn, nextRowLabel);
            string value = affected ? string.Empty : RemapParentReference(previous.GetValueOrDefault(cell.UserTextKey, string.Empty), axis, splitIndex);
            _values[nextKey] = value;
        }
        int oldRows = _layout.RowCount;
        int oldColumns = _layout.ColumnCount;
        if (axis == PanelExtrusionAxis.Vertical)
        {
            for (int row = 0; row < oldRows; row++)
            {
                string label = PanelCladdingKeyService.GetRowLabel(row);
                PanelCladdingCell? source = previousCells.FirstOrDefault(cell => cell.Column == splitIndex && cell.Row == row);
                bool affected = source is not null && (affectedCellKeys is null || affectedCellKeys.Contains(source.UserTextKey));
                string sourceValue = source is null ? string.Empty : previous.GetValueOrDefault(source.UserTextKey, string.Empty);
                _values[PanelCladdingKeyService.GetCellKey(splitIndex + 1, label)] = affected
                    ? string.Empty
                    : RemapParentReference(sourceValue, axis, splitIndex);
            }
        }
        else
        {
            string newLabel = PanelCladdingKeyService.GetRowLabel(splitIndex + 1);
            for (int column = 0; column < oldColumns; column++)
            {
                PanelCladdingCell? source = previousCells.FirstOrDefault(cell => cell.Column == column && cell.Row == splitIndex);
                bool affected = source is not null && (affectedCellKeys is null || affectedCellKeys.Contains(source.UserTextKey));
                string sourceValue = source is null ? string.Empty : previous.GetValueOrDefault(source.UserTextKey, string.Empty);
                _values[PanelCladdingKeyService.GetCellKey(column, newLabel)] = affected
                    ? string.Empty
                    : RemapParentReference(sourceValue, axis, splitIndex);
            }
        }
    }

    private static string RemapParentReference(string value, PanelExtrusionAxis axis, int splitIndex)
    {
        if (!PanelCladdingKeyService.IsCellLabelToken(value))
        {
            return value;
        }
        int digitCount = value.TakeWhile(char.IsDigit).Count();
        if (digitCount == 0 || !int.TryParse(value[..digitCount], out int column))
        {
            return value;
        }
        string rowLabel = value[digitCount..].ToUpperInvariant();
        int row = RowIndex(rowLabel);
        if (axis == PanelExtrusionAxis.Vertical && column > splitIndex)
        {
            column++;
        }
        if (axis == PanelExtrusionAxis.Horizontal && row > splitIndex)
        {
            row++;
            rowLabel = PanelCladdingKeyService.GetRowLabel(row);
        }
        return $"{column}{rowLabel}";
    }

    private static int RowIndex(string label)
    {
        int value = 0;
        foreach (char character in label)
        {
            value = value * 26 + (character - 'A' + 1);
        }
        return value - 1;
    }

    private void RemapExtrusionTopologyForInsertedMullion(PanelExtrusionAxis insertedAxis, int splitIndex)
    {
        PanelExtrusionAxis affectedAxis = insertedAxis == PanelExtrusionAxis.Horizontal
            ? PanelExtrusionAxis.Vertical
            : PanelExtrusionAxis.Horizontal;
        string[] deleted = _deletedExtrusions.SelectMany(id => RemapAtomicId(id, affectedAxis, splitIndex)).ToArray();
        _deletedExtrusions.Clear();
        _deletedExtrusions.UnionWith(deleted);
        string[] hidden = _hiddenExtrusions.SelectMany(id => RemapAtomicId(id, affectedAxis, splitIndex)).ToArray();
        _hiddenExtrusions.Clear();
        _hiddenExtrusions.UnionWith(hidden);
        for (int index = 0; index < _mergedGroups.Count; index++)
        {
            PanelExtrusionMergeGroup group = _mergedGroups[index];
            string[] atomic = group.AtomicIds.SelectMany(id => RemapAtomicId(id, affectedAxis, splitIndex))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string id = $"M:{group.Axis}:{group.Offset.ToString("0.#####", CultureInfo.InvariantCulture)}:{string.Join('+', atomic)}";
            _mergedGroups[index] = new PanelExtrusionMergeGroup(id, group.Axis, group.Offset, atomic);
        }
        RemapFrameAssignmentsForInsertedMullion(ToTopologyAxis(insertedAxis), splitIndex);
    }

    private static IEnumerable<string> RemapAtomicId(string id, PanelExtrusionAxis affectedAxis, int splitIndex)
    {
        if (!PanelExtrusionTopology.TryParseAtomicId(id, out PanelExtrusionAxis axis, out double offset, out int bay) || axis != affectedAxis)
        {
            return [id];
        }
        if (bay < splitIndex)
        {
            return [id];
        }
        if (bay > splitIndex)
        {
            return [PanelExtrusionTopology.AtomicId(axis, offset, bay + 1)];
        }
        return [PanelExtrusionTopology.AtomicId(axis, offset, bay), PanelExtrusionTopology.AtomicId(axis, offset, bay + 1)];
    }

    private void SplitDimensionLock(PanelExtrusionAxis axis, int splitIndex)
    {
        List<bool> locks = axis == PanelExtrusionAxis.Horizontal ? _rowLocks : _columnLocks;
        bool prior = splitIndex >= 0 && splitIndex < locks.Count && locks[splitIndex];
        if (splitIndex >= 0 && splitIndex < locks.Count)
        {
            locks.RemoveAt(splitIndex);
        }
        locks.Insert(splitIndex, prior);
        locks.Insert(splitIndex + 1, false);
    }

    private void OnDimensionValueCommitted(object? sender, PanelDimensionEditEventArgs e)
    {
        if (_layout is null)
        {
            return;
        }
        IReadOnlyList<bool> locks = e.Axis == PanelDimensionAxis.Column ? _columnLocks : _rowLocks;
        if (e.Index < 0 || e.Index >= locks.Count || locks[e.Index])
        {
            ShowToast("Unlock this dimension before editing it");
            return;
        }
        double total = e.Axis == PanelDimensionAxis.Column ? _layout.Width : _layout.Height;
        double minimum = Math.Min(4d, total / Math.Max(2d, locks.Count * 2d));
        double maximum = Math.Max(minimum, total - minimum * (locks.Count - 1));
        if (e.Value < minimum || e.Value > maximum)
        {
            ShowToast($"Enter a value from {minimum:0.#####} to {maximum:0.#####}");
            return;
        }
        UpdateDimension(e.Axis, e.Index, e.Value, minimum);
    }

    private void OnDimensionLockToggleRequested(object? sender, PanelDimensionEditEventArgs e)
    {
        List<bool> locks = e.Axis == PanelDimensionAxis.Column ? _columnLocks : _rowLocks;
        if (e.Index < 0 || e.Index >= locks.Count)
        {
            return;
        }
        PushUndoState();
        locks[e.Index] = !locks[e.Index];
        MarkStructuralDirty();
        PanelGridCanvas.SetDimensionLocks(_columnLocks, _rowLocks);
        ShowToast($"{(e.Axis == PanelDimensionAxis.Column ? "Column" : "Row")} dimension {(locks[e.Index] ? "locked" : "unlocked")}");
    }

    private void UpdateDimension(PanelDimensionAxis axis, int index, double nextValue, double minimum)
    {
        if (_layout is null)
        {
            return;
        }
        List<double> offsets = axis == PanelDimensionAxis.Column ? _verticalOffsets : _horizontalOffsets;
        List<bool> locks = axis == PanelDimensionAxis.Column ? _columnLocks : _rowLocks;
        double total = axis == PanelDimensionAxis.Column ? _layout.Width : _layout.Height;
        List<double> dimensions = SegmentLengths(total, offsets).ToList();
        int[] recipients = Enumerable.Range(0, dimensions.Count).Where(candidate => candidate != index && !locks[candidate]).ToArray();
        if (index < 0 || index >= dimensions.Count || locks[index])
        {
            ShowToast("Unlock this dimension before editing it");
            return;
        }
        if (recipients.Length == 0)
        {
            ShowToast("Unlock another dimension so the total size can stay fixed");
            return;
        }
        double delta = nextValue - dimensions[index];
        if (Math.Abs(delta) < 0.000005d)
        {
            return;
        }
        if (delta > 0d)
        {
            double capacity = recipients.Sum(candidate => Math.Max(0d, dimensions[candidate] - minimum));
            if (delta > capacity + 0.001d)
            {
                ShowToast("The unlocked dimensions cannot shrink enough to preserve the total");
                return;
            }
            PushUndoState();
            foreach (int candidate in recipients)
            {
                dimensions[candidate] -= delta * ((dimensions[candidate] - minimum) / Math.Max(capacity, 1e-9d));
            }
        }
        else
        {
            PushUndoState();
            double weight = recipients.Sum(candidate => dimensions[candidate]);
            foreach (int candidate in recipients)
            {
                dimensions[candidate] += (-delta) * (dimensions[candidate] / Math.Max(weight, 1e-9d));
            }
        }
        dimensions[index] = nextValue;
        for (int dimension = 0; dimension < dimensions.Count; dimension++)
        {
            dimensions[dimension] = Math.Round(dimensions[dimension], 5, MidpointRounding.AwayFromZero);
        }
        double difference = Math.Round(total - dimensions.Sum(), 5, MidpointRounding.AwayFromZero);
        dimensions[recipients[^1]] = Math.Round(dimensions[recipients[^1]] + difference, 5, MidpointRounding.AwayFromZero);
        offsets.Clear();
        double cursor = 0d;
        for (int dimension = 0; dimension < dimensions.Count - 1; dimension++)
        {
            cursor += dimensions[dimension];
            offsets.Add(Math.Round(cursor, 5, MidpointRounding.AwayFromZero));
        }
        _mergedGroups.Clear();
        _deletedExtrusions.Clear();
        _hiddenExtrusions.Clear();
        _selectedExtrusions.Clear();
        _layout = BuildWorkingLayout();
        MarkStructuralDirty();
        PopulatePanelMetadata();
        RefreshCanvas();
        if (_activeView == PanelEditorView.Extrusion)
        {
            UpdateExtrusionSelectionUi();
        }
        ShowToast("Dimension updated · overall panel size preserved");
    }

    private static IReadOnlyList<double> SegmentLengths(double total, IReadOnlyList<double> offsets)
    {
        double[] positions = [0d, .. offsets.OrderBy(value => value), total];
        return positions.Zip(positions.Skip(1), (start, end) => Math.Max(1e-9d, end - start)).ToArray();
    }

    private void MarkStructuralDirty()
    {
        _structuralDirty = true;
        _claddingDirty = true;
        RefreshDirtyState();
        FooterStatusText.Text = "Structural changes not saved";
        UpdateWorkbookStatus();
    }

    private void MarkCladdingDirty()
    {
        _claddingDirty = true;
        RefreshDirtyState();
        FooterStatusText.Text = "Cladding changes not saved";
        UpdateWorkbookStatus();
    }

    private void MarkExtrusionAssignmentDirty()
    {
        _structuralDirty = true;
        RefreshDirtyState();
        FooterStatusText.Text = "Extrusion assignments not saved";
        UpdateWorkbookStatus();
    }

    private void UpdateTypePreview()
    {
        if (_layout is null)
        {
            TypeCodeText.Text = "TYPE-";
            return;
        }
        PanelCladdingLayout previewLayout = BuildWorkingLayout();
        IReadOnlyDictionary<string, string> expandedValues = _logicalCells.Expand(
            previewLayout.Cells,
            previewLayout.Topology,
            _values);
        OperationResponse<PanelCladdingTypeIdentity> response = _controller.PreviewType(
            previewLayout,
            expandedValues,
            previewLayout.SystemCode);
        TypeCodeText.Text = response.Success && response.Data is not null
            ? response.Data.TypeCode
            : $"{NormalizeSystemCode(_layout.SystemCode)}-{_layout.ColumnCount}X{_layout.RowCount}-PENDING";
        TypeCodeText.ToolTip = response.Success ? null : response.Message;
        UpdateWorkbookStatus();
    }

    private void UpdateFrameTypologyPreview()
    {
        if (_layout is null)
        {
            FrameTypologyText.Text = "UNASSIGNED";
            return;
        }
        PanelFrameAssignmentState assignments = CaptureFrameAssignmentState();
        if (assignments.IsEmpty)
        {
            FrameTypologyText.Text = "UNASSIGNED";
            FrameTypologyText.ToolTip = "Assign at least one 1D extrusion code to calculate the frame typology.";
            return;
        }
        PanelCladdingLayout previewLayout = BuildWorkingLayout();
        OperationResponse<PanelFrameTypologyIdentity> response = _controller.PreviewFrameTypology(
            previewLayout,
            assignments,
            previewLayout.SystemCode);
        FrameTypologyText.Text = response.Success && response.Data is not null
            ? response.Data.TypologyCode
            : $"{NormalizeSystemCode(_layout.SystemCode)}-{_layout.ColumnCount}X{_layout.RowCount}-PENDING";
        FrameTypologyText.ToolTip = response.Success ? null : response.Message;
    }

    private static string NormalizeSystemCode(string value)
    {
        string normalized = new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "PANEL" : normalized[..Math.Min(10, normalized.Length)];
    }

    private void UpdateWorkbookStatus()
    {
        SetSaveButtonsEnabled(_layout?.CanSave == true);
    }

    private void SetSaveButtonsEnabled(bool enabled)
    {
        SaveExtrusionsButton.IsEnabled = enabled;
        SaveCladdingButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
    }

    private void OnSaveExtrusionsClick(object sender, RoutedEventArgs e) =>
        SavePanel(PanelCladdingSaveScope.Extrusions);

    private void OnSaveCladdingClick(object sender, RoutedEventArgs e) =>
        SavePanel(PanelCladdingSaveScope.Cladding);

    private void OnSaveClick(object sender, RoutedEventArgs e) =>
        SavePanel(PanelCladdingSaveScope.Both);

    private void SavePanel(PanelCladdingSaveScope scope)
    {
        if (_layout is null || _sourceLayout is null)
        {
            return;
        }
        if (scope == PanelCladdingSaveScope.Cladding && _structuralDirty)
        {
            const string message =
                "Extrusion edits are still unsaved. Use Save Extrusions first, or use Save Both.";
            SetStatus(message, error: true);
            MessageBox.Show(this, message, "Panel Cladding Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetSaveButtonsEnabled(false);
        FooterStatusText.Text = "Saving...";
        try
        {
            OperationResponse<PanelCladdingSaveResult> response = _controller.Save(new PanelCladdingSaveRequest
            {
                FilePath = _sourceLayout.DocumentPath,
                ObjectId = _sourceLayout.ObjectId,
                ExpectedGeometryFingerprint = _sourceLayout.GeometryFingerprint,
                WorkbookPath = _workbookPath,
                AllowCreateWorkbook = false,
                SystemCode = _sourceLayout.SystemCode,
                HorizontalOffsets = _horizontalOffsets.ToArray(),
                VerticalOffsets = _verticalOffsets.ToArray(),
                Topology = CaptureTopologyState(),
                FrameAssignments = CaptureFrameAssignmentState(),
                CellValues = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase),
                Scope = scope
            });
            if (!response.Success || response.Data is null)
            {
                SetStatus(response.Message, error: true);
                MessageBox.Show(this, response.Message, "Panel Cladding Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _workbookPath = response.Data.WorkbookPath;
            if ((scope is PanelCladdingSaveScope.Cladding or PanelCladdingSaveScope.Both) &&
                !string.IsNullOrWhiteSpace(response.Data.TypeCode))
            {
                TypeCodeText.Text = response.Data.TypeCode;
            }
            if (scope is PanelCladdingSaveScope.Extrusions or PanelCladdingSaveScope.Both)
            {
                FrameTypologyText.Text = string.IsNullOrWhiteSpace(response.Data.FrameTypology)
                    ? "UNASSIGNED"
                    : response.Data.FrameTypology;
            }

            _structuralDirty = scope == PanelCladdingSaveScope.Cladding && _structuralDirty;
            _claddingDirty = scope == PanelCladdingSaveScope.Extrusions && _claddingDirty;
            RefreshDirtyState();

            if (scope == PanelCladdingSaveScope.Both)
            {
                OperationResponse reload = LoadPanel(_sourceLayout.DocumentPath, _sourceLayout.ObjectId);
                if (!reload.Success)
                {
                    SetStatus($"Saved, but the editor could not reload the panel: {reload.Message}", error: true);
                    return;
                }
            }
            else
            {
                OperationResponse refresh = RefreshSourceAfterPartialSave();
                if (!refresh.Success)
                {
                    SetStatus($"Saved, but the editor could not refresh the panel: {refresh.Message}", error: true);
                    return;
                }
            }

            string savedLabel = scope switch
            {
                PanelCladdingSaveScope.Extrusions => "extrusion data",
                PanelCladdingSaveScope.Cladding => "cladding data",
                _ => "extrusion and cladding data"
            };
            string outstanding = _dirty
                ? scope == PanelCladdingSaveScope.Extrusions
                    ? " Cladding changes are still not saved."
                    : " Extrusion changes are still not saved."
                : string.Empty;
            SetStatus($"Saved {savedLabel} to the panel.{outstanding}", error: false);
            ShowToast($"Saved {savedLabel} to Rhino");
        }
        finally
        {
            UpdateWorkbookStatus();
        }
    }

    private OperationResponse RefreshSourceAfterPartialSave()
    {
        if (_sourceLayout is null)
        {
            return OperationResponse.Fail("PANEL_CLADDING_EDITOR_SOURCE_REQUIRED");
        }
        OperationResponse<PanelCladdingLayout> response =
            _controller.Load(_sourceLayout.DocumentPath, _sourceLayout.ObjectId);
        if (!response.Success || response.Data is null)
        {
            return OperationResponse.Fail(response.Message);
        }

        _sourceLayout = response.Data;
        _workbookPath = response.Data.WorkbookPath;
        _layout = BuildWorkingLayout();
        PopulatePanelMetadata();
        RefreshCanvas();
        if (_activeView == PanelEditorView.Extrusion)
        {
            UpdateExtrusionSelectionUi();
        }
        else
        {
            UpdateSelectionUi(syncAssignmentMode: true);
        }
        return OperationResponse.Ok();
    }

    private void OnManageMaterialsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new MaterialSetupDialog(_controller, _workbookPath, _materials) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _workbookPath = dialog.WorkbookPath;
            _materials.Clear();
            foreach (PanelCladdingMaterial material in dialog.Materials.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
            {
                _materials.Add(material.Copy());
            }
            if (_sourceLayout is not null && !string.IsNullOrWhiteSpace(_workbookPath))
            {
                OperationResponse<string> associated =
                    _controller.SetWorkbookPath(_sourceLayout.DocumentPath, _workbookPath);
                if (!associated.Success)
                {
                    SetStatus(associated.Message, error: true);
                    MessageBox.Show(this, associated.Message, "Material setup",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            RefreshCanvas();
            UpdateSelectionUi(syncAssignmentMode: true);
            ShowToast("Project material catalogue saved");
        }
    }

    private void OnManageExtrusionsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ExtrusionSetupDialog(_controller, _workbookPath, _frameExtrusions)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        _workbookPath = dialog.WorkbookPath;
        _frameExtrusions.Clear();
        foreach (PanelFrameExtrusion extrusion in dialog.Extrusions
                     .Where(item => item.IsConfigured)
                     .OrderBy(item => item.SourcePageNumber)
                     .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
        {
            _frameExtrusions.Add(extrusion.Copy());
        }
        HashSet<string> assignedCodes = _frameAssignmentCodes.Values
            .SelectMany(codes => codes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (PanelFrameExtrusion extrusion in _frameExtrusions.Where(item => assignedCodes.Contains(item.Code)))
        {
            _frameDefinitions[extrusion.Code] = extrusion.ToDefinition();
        }
        if (assignedCodes.Count > 0)
        {
            MarkExtrusionAssignmentDirty();
            RefreshCanvas();
        }
        EnsureFrameExtrusionLibrary();
        if (_sourceLayout is not null && !string.IsNullOrWhiteSpace(_workbookPath))
        {
            OperationResponse<string> associated =
                _controller.SetWorkbookPath(_sourceLayout.DocumentPath, _workbookPath);
            if (!associated.Success)
            {
                SetStatus(associated.Message, error: true);
                MessageBox.Show(this, associated.Message, "Extrusion setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        UpdateExtrusionSelectionUi();
        ShowToast("Project extrusion catalogue saved");
    }

    private void OnMaterialChipMouseDown(object sender, MouseButtonEventArgs e) => _materialDragStart = e.GetPosition(this);

    private void OnMaterialChipMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element ||
            element.DataContext is not PanelCladdingMaterial material)
        {
            return;
        }
        Vector delta = e.GetPosition(this) - _materialDragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        DragDrop.DoDragDrop(element, new DataObject(DataFormats.Text, material.Code), DragDropEffects.Copy);
    }

    private void OnMaterialChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is PanelCladdingMaterial material)
        {
            SwitchAssignmentMode(parentMode: false);
            MaterialSelect.SelectedItem = material;
            if (_selectedKeys.Count == 0)
            {
                ShowToast("Select one or more cells first");
            }
        }
    }

    private void OnExtrusionTileMouseDown(object sender, MouseButtonEventArgs e) =>
        _extrusionDragStart = e.GetPosition(this);

    private void OnExtrusionTileMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not FrameworkElement { DataContext: PanelFrameExtrusion extrusion } element)
        {
            return;
        }
        Vector delta = e.GetPosition(this) - _extrusionDragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }
        var data = new DataObject();
        data.SetData(FrameExtrusionDragFormat, extrusion.Code);
        data.SetData(DataFormats.Text, extrusion.Code);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    private void OnZoomChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ZoomCombo.SelectedItem is not ComboBoxItem item ||
            !double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double zoom))
        {
            return;
        }
        PanelGridCanvas.SetZoom(zoom);
    }

    private void OnCanvasZoomChanged(object? sender, PanelZoomChangedEventArgs e)
    {
        ComboBoxItem? match = ZoomCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
            double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            Math.Abs(value - e.Zoom) < 1e-9d);
        if (match is not null && !ReferenceEquals(ZoomCombo.SelectedItem, match))
        {
            ZoomCombo.SelectedItem = match;
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            UndoLastChange();
            e.Handled = true;
        }
        else if ((e.Key == Key.Delete || e.Key == Key.Back) &&
                 Keyboard.FocusedElement is not TextBox && Keyboard.FocusedElement is not ComboBox)
        {
            if (_activeView == PanelEditorView.Extrusion)
            {
                OnDeleteExtrusionsClick(sender, e);
            }
            else
            {
                AssignSelected(string.Empty, "Assignment cleared");
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _activeView == PanelEditorView.Extrusion)
        {
            _selectedExtrusions.Clear();
            UpdateExtrusionSelectionUi();
            e.Handled = true;
        }
    }

    private void RefreshDirtyState() => SetDirty(_structuralDirty || _claddingDirty);

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        DirtyBadgeText.Text = dirty ? "Unsaved" : "Saved";
        DirtyBadge.Background = (Brush)FindResource(dirty ? "WarningSoftBrush" : "SuccessSoftBrush");
        FooterStatusText.Text = dirty ? "Changes not saved" : "All changes saved";
    }

    private void SetStatus(string message, bool error)
    {
        FooterStatusText.Text = message;
        FooterStatusText.Foreground = (Brush)FindResource(error ? "DangerBrush" : "MutedBrush");
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private bool ConfirmDiscardChanges() =>
        MessageBox.Show(this, "Discard unsaved panel cladding edits?", "Panel Cladding Editor",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_isLoading && _dirty && !ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private sealed record EditorSnapshot(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyDictionary<string, IReadOnlyList<string>> FrameAssignments,
        IReadOnlyDictionary<string, PanelFrameProfileDefinition> FrameDefinitions,
        IReadOnlyDictionary<string, double> FrameModifiers,
        IReadOnlyList<double> HorizontalOffsets,
        IReadOnlyList<double> VerticalOffsets,
        IReadOnlyList<bool> RowLocks,
        IReadOnlyList<bool> ColumnLocks,
        IReadOnlyList<PanelExtrusionMergeGroup> MergedGroups,
        IReadOnlyList<string> DeletedExtrusions,
        IReadOnlyList<string> HiddenExtrusions,
        IReadOnlyList<string> SelectedKeys,
        IReadOnlyList<string> SelectedExtrusions,
        bool StructuralDirty,
        bool CladdingDirty);
}

public sealed record PanelOffsetDisplay(
    string Code,
    string Display,
    PanelDimensionAxis Axis,
    int Index,
    double Value,
    string Unit);
