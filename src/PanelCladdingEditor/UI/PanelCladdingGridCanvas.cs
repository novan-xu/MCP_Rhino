using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public sealed class PanelCladdingGridCanvas : Canvas
{
    private static readonly Brush Foreground = FrozenBrush("#263239");
    private static readonly Brush Muted = FrozenBrush("#748087");
    private static readonly Brush Border = FrozenBrush("#DDE3E7");
    private static readonly Brush BorderStrong = FrozenBrush("#AEB8BE");
    private static readonly Brush Surface = FrozenBrush("#FFFFFF");
    private static readonly Brush CanvasSurface = FrozenBrush("#FAFBFC");
    private static readonly Brush CanvasGrid = FrozenBrush("#E2E7EA");
    private static readonly Brush GridLine = FrozenBrush("#364248");
    private static readonly Brush Accent = FrozenBrush("#229447");
    private static readonly Brush Selection = FrozenBrush("#FFEA00");
    private static readonly Brush Error = FrozenBrush("#FF1F1F");
    private static readonly Brush Hidden = FrozenBrush("#D97706");
    private static readonly Brush HiddenSoft = FrozenBrush("#FFF1D6");
    private static readonly Brush ExtrusionLabel = FrozenBrush("#EEF2FF");
    private static readonly Brush ExtrusionLabelText = FrozenBrush("#000000");
    private static readonly Pen ThinBorderPen = FrozenPen(BorderStrong, 1d);
    private static readonly Pen CanvasGridPen = FrozenPen(CanvasGrid, 0.7d);
    private static readonly Typeface BodyTypeface = new("Segoe UI");
    private static readonly Typeface MonoTypeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private readonly Dictionary<string, Rect> _cellRects = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Key, Rect Rect)> _cellHitRects = [];
    private readonly Dictionary<string, Rect> _segmentHitRects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Rect> _segmentLabelRects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(PanelDimensionAxis Axis, int Index), Rect> _dimensionBadges = new();
    private readonly Dictionary<(PanelDimensionAxis Axis, int Index), Rect> _dimensionLocks = new();
    private PanelCladdingLayout? _layout;
    private IReadOnlyDictionary<string, string> _values = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, PanelCladdingMaterial> _materials = new Dictionary<string, PanelCladdingMaterial>();
    private HashSet<string> _selectedKeys = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _selectedExtrusions = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PanelExtrusionMergeGroup> _mergedGroups = Array.Empty<PanelExtrusionMergeGroup>();
    private IReadOnlySet<string> _deletedExtrusions = new HashSet<string>();
    private IReadOnlySet<string> _hiddenExtrusions = new HashSet<string>();
    private IReadOnlyList<PanelExtrusionSegment> _visibleExtrusions = Array.Empty<PanelExtrusionSegment>();
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _extrusionAssignments =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<bool> _columnLocks = Array.Empty<bool>();
    private IReadOnlyList<bool> _rowLocks = Array.Empty<bool>();
    private PanelEditorView _view = PanelEditorView.Cladding;
    private PanelExtrusionAxis? _placementAxis;
    private string? _hoverKey;
    private Point _mouseDownPoint;
    private bool _isPointerDown;
    private bool _isMarquee;
    private Rect _marqueeRect;
    private ModifierKeys _mouseDownModifiers;
    private Border? _dimensionEditor;
    private TextBox? _dimensionEditorTextBox;
    private PanelDimensionAxis _dimensionEditorAxis;
    private int _dimensionEditorIndex;
    private bool _dimensionEditorClosing;
    private bool _isPanning;
    private Point _panStartPoint;
    private Vector _panStartOffset;
    private Vector _panOffset;

    public const double MinimumZoom = 0.8d;
    public const double MaximumZoom = 1.5d;
    public const double ZoomStep = 0.05d;
    public const double PanelFrameStroke = 2d;
    public const double PanelDividerStroke = 1.5d;
    public const double SelectionHaloBandWidth = 2d;
    public const double SelectionHaloGap = 0.75d;
    public const double DimensionLineOffset = 60d;
    private const double HorizontalPanelClearance = 150d;
    private const double VerticalPanelClearance = 110d;

    public PanelCladdingGridCanvas()
    {
        Focusable = true;
        AllowDrop = true;
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        PreviewMouseWheel += OnPreviewMouseWheel;
        MouseLeave += (_, _) =>
        {
            if (!_isPointerDown)
            {
                _hoverKey = null;
                InvalidateVisual();
            }
        };
        DragOver += OnDragOver;
        DragLeave += (_, _) =>
        {
            _hoverKey = null;
            InvalidateVisual();
        };
        Drop += OnDrop;
    }

    public event EventHandler<IReadOnlyCollection<string>>? SelectionChanged;
    public event EventHandler<IReadOnlyCollection<string>>? ExtrusionSelectionChanged;
    public event EventHandler<PanelMaterialDropEventArgs>? MaterialDropped;
    public event EventHandler<PanelDimensionEditEventArgs>? DimensionValueCommitted;
    public event EventHandler<PanelDimensionEditEventArgs>? DimensionLockToggleRequested;
    public event EventHandler<PanelMullionPlacementEventArgs>? MullionPlacementRequested;
    public event EventHandler<PanelZoomChangedEventArgs>? ZoomChanged;

    public double Zoom { get; private set; } = 1d;

    public Vector PanOffset => _panOffset;

    public PanelEditorView ActiveView => _view;

    public IReadOnlyList<PanelExtrusionSegment> VisibleExtrusions => _visibleExtrusions;

    public void SetExtrusionAssignments(
        IReadOnlyDictionary<string, IReadOnlyList<string>> assignments)
    {
        _extrusionAssignments = assignments;
        InvalidateVisual();
    }

    public bool IsDimensionEditorOpen => _dimensionEditor is not null;

    public void SetZoom(double zoom)
    {
        Zoom = Math.Round(Math.Clamp(zoom, MinimumZoom, MaximumZoom) / ZoomStep) * ZoomStep;
        ClampPanOffset();
        InvalidateVisual();
    }

    public void PanBy(Vector delta)
    {
        _panOffset += delta;
        ClampPanOffset();
        InvalidateVisual();
    }

    public void SetLayout(
        PanelCladdingLayout layout,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, PanelCladdingMaterial> materials,
        IEnumerable<string> selectedKeys)
    {
        CancelInlineDimensionEdit();
        bool panelChanged = _layout?.ObjectId != layout.ObjectId;
        _layout = layout;
        _values = values;
        _materials = materials;
        _selectedKeys = selectedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (panelChanged)
        {
            _panOffset = default;
        }
        ClampPanOffset();
        InvalidateVisual();
    }

    public void SetValues(
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, PanelCladdingMaterial> materials)
    {
        _values = values;
        _materials = materials;
        InvalidateVisual();
    }

    public void SetSelection(IEnumerable<string> selectedKeys)
    {
        _selectedKeys = selectedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        InvalidateVisual();
    }

    public void SetExtrusionState(
        PanelEditorView view,
        IEnumerable<string> selectedExtrusions,
        IReadOnlyList<PanelExtrusionMergeGroup> mergedGroups,
        IReadOnlySet<string> deletedExtrusions,
        IReadOnlySet<string>? hiddenExtrusions = null)
    {
        _view = view;
        _selectedExtrusions = selectedExtrusions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _mergedGroups = mergedGroups;
        _deletedExtrusions = deletedExtrusions;
        _hiddenExtrusions = hiddenExtrusions ?? new HashSet<string>();
        AllowDrop = view == PanelEditorView.Cladding;
        InvalidateVisual();
    }

    public void SetDimensionLocks(IReadOnlyList<bool> columnLocks, IReadOnlyList<bool> rowLocks)
    {
        _columnLocks = columnLocks;
        _rowLocks = rowLocks;
        InvalidateVisual();
    }

    public void SetMullionPlacement(PanelExtrusionAxis? axis)
    {
        _placementAxis = axis;
        Cursor = axis is null ? Cursors.Arrow : Cursors.Cross;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(CanvasSurface, null, new Rect(RenderSize));
        _cellRects.Clear();
        _cellHitRects.Clear();
        _segmentHitRects.Clear();
        _segmentLabelRects.Clear();
        _dimensionBadges.Clear();
        _dimensionLocks.Clear();
        if (_layout is null || _layout.Cells.Count == 0 || ActualWidth < 240d || ActualHeight < 220d)
        {
            DrawText(drawingContext, "Select a panel to edit cladding assignments.", Muted, 13d, new Point(24d, 24d), BodyTypeface);
            return;
        }

        ClampPanOffset();
        Rect panelBounds = CalculatePanelBounds(_layout);
        DrawModelGrid(drawingContext, panelBounds);
        IReadOnlyList<double> columnWidths = SegmentLengths(_layout.Width, _layout.VerticalOffsets);
        IReadOnlyList<double> rowHeights = SegmentLengths(_layout.Height, _layout.HorizontalOffsets);
        double totalWidth = Math.Max(1e-9d, columnWidths.Sum());
        double totalHeight = Math.Max(1e-9d, rowHeights.Sum());

        DrawDimensionLine(drawingContext, panelBounds, columnWidths, totalWidth, PanelDimensionAxis.Column);
        DrawDimensionLine(drawingContext, panelBounds, rowHeights, totalHeight, PanelDimensionAxis.Row);
        drawingContext.DrawRectangle(GridLine, null, panelBounds);

        double[] xPositions = BuildPositions(panelBounds.Left, panelBounds.Width, columnWidths);
        double[] yPositions = BuildPositions(panelBounds.Top, panelBounds.Height, rowHeights.Reverse().ToArray());
        PanelCladdingCellTopology topology = PanelCladdingCellTopology.Build(_layout, _deletedExtrusions);
        foreach (PanelCladdingCellGroup group in topology.Groups)
        {
            if (group.IsRectangular)
            {
                Rect groupRect = CellGroupRect(group, xPositions, yPositions);
                foreach (PanelCladdingCell member in group.Cells)
                {
                    _cellRects[member.UserTextKey] = groupRect;
                }
                _cellHitRects.Add((group.Representative.UserTextKey, groupRect));
                DrawCell(drawingContext, group.Representative, groupRect, drawContent: true);
                continue;
            }
            foreach (PanelCladdingCell member in group.Cells)
            {
                Rect fragmentRect = CellFragmentRect(group, member, xPositions, yPositions);
                _cellRects[member.UserTextKey] = fragmentRect;
                _cellHitRects.Add((group.Representative.UserTextKey, fragmentRect));
                DrawCell(
                    drawingContext,
                    group.Representative,
                    fragmentRect,
                    drawContent: string.Equals(
                        member.UserTextKey,
                        group.Representative.UserTextKey,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        if (_view == PanelEditorView.Cladding)
        {
            DrawParentBoundaries(drawingContext, topology, xPositions, yPositions);
            _visibleExtrusions = Array.Empty<PanelExtrusionSegment>();
        }
        else
        {
            _visibleExtrusions = PanelExtrusionTopology.Build(
                _layout.Width,
                _layout.Height,
                _layout.HorizontalOffsets,
                _layout.VerticalOffsets,
                _mergedGroups,
                _deletedExtrusions);
            DrawExtrusionLayout(drawingContext, panelBounds, _visibleExtrusions);
        }
        if (_isMarquee)
        {
            var marqueeFill = new SolidColorBrush(Color.FromArgb(30, 34, 148, 71));
            marqueeFill.Freeze();
            drawingContext.DrawRectangle(marqueeFill, new Pen(Accent, 1d), _marqueeRect);
        }
    }

    private Rect CalculatePanelBounds(PanelCladdingLayout layout)
    {
        double availableWidth = Math.Max(220d, ActualWidth - HorizontalPanelClearance * 2d);
        double availableHeight = Math.Max(160d, ActualHeight - VerticalPanelClearance * 2d);
        Size size = CalculatePanelSize(layout, Zoom, availableWidth, availableHeight);
        double left = (ActualWidth - size.Width) / 2d + _panOffset.X;
        double top = (ActualHeight - size.Height) / 2d + _panOffset.Y;
        return new Rect(left, top, size.Width, size.Height);
    }

    private static Size CalculatePanelSize(
        PanelCladdingLayout layout,
        double zoom,
        double availableWidth,
        double availableHeight)
    {
        double ratio = layout.Width > 1e-9d && layout.Height > 1e-9d
            ? layout.Width / layout.Height
            : 1.7d;
        if (!double.IsFinite(ratio) || ratio <= 1e-9d)
        {
            ratio = 1.7d;
        }
        double baseWidth = Math.Min(availableWidth, availableHeight * ratio);
        double uniformScale = double.IsFinite(zoom) ? Math.Max(zoom, 1e-9d) : 1d;
        double scaledWidth = baseWidth * uniformScale;
        return new Size(scaledWidth, scaledWidth / ratio);
    }

    private void ClampPanOffset()
    {
        if (_layout is null || ActualWidth < 1d || ActualHeight < 1d)
        {
            _panOffset = default;
            return;
        }
        double availableWidth = Math.Max(220d, ActualWidth - HorizontalPanelClearance * 2d);
        double availableHeight = Math.Max(160d, ActualHeight - VerticalPanelClearance * 2d);
        Size minimum = CalculatePanelSize(_layout, MinimumZoom, availableWidth, availableHeight);
        Size current = CalculatePanelSize(_layout, Zoom, availableWidth, availableHeight);
        double maximumX = Math.Max(0d, (current.Width - minimum.Width) / 2d);
        double maximumY = Math.Max(0d, (current.Height - minimum.Height) / 2d);
        _panOffset = new Vector(
            Math.Clamp(_panOffset.X, -maximumX, maximumX),
            Math.Clamp(_panOffset.Y, -maximumY, maximumY));
    }

    private void DrawModelGrid(DrawingContext dc, Rect panel)
    {
        if (_layout is null || _layout.Width <= 1e-9d || _layout.ModelUnitScaleToMillimeters <= 1e-9d)
        {
            return;
        }
        double tenInchesInModelUnits = 254d / _layout.ModelUnitScaleToMillimeters;
        double spacing = panel.Width * tenInchesInModelUnits / _layout.Width;
        if (!double.IsFinite(spacing) || spacing < 4d)
        {
            return;
        }
        double startX = panel.Left % spacing;
        if (startX < 0d)
        {
            startX += spacing;
        }
        for (double x = startX; x <= ActualWidth; x += spacing)
        {
            dc.DrawLine(CanvasGridPen, new Point(x, 0d), new Point(x, ActualHeight));
        }
        double startY = panel.Bottom % spacing;
        if (startY < 0d)
        {
            startY += spacing;
        }
        for (double y = startY; y <= ActualHeight; y += spacing)
        {
            dc.DrawLine(CanvasGridPen, new Point(0d, y), new Point(ActualWidth, y));
        }
    }

    private void DrawDimensionLine(
        DrawingContext dc,
        Rect panel,
        IReadOnlyList<double> modelLengths,
        double total,
        PanelDimensionAxis axis)
    {
        bool horizontal = axis == PanelDimensionAxis.Column;
        double axisLength = horizontal ? panel.Width : panel.Height;
        double cursor = horizontal ? panel.Left : panel.Top;
        if (horizontal)
        {
            double y = panel.Top - DimensionLineOffset;
            dc.DrawLine(ThinBorderPen, new Point(panel.Left, y), new Point(panel.Right, y));
            for (int index = 0; index < modelLengths.Count; index++)
            {
                double length = modelLengths[index];
                double segment = axisLength * length / total;
                DrawDimensionBadge(dc, FormatModelLength(length), new Point(cursor + segment / 2d, y - 27d), axis, index);
                dc.DrawLine(ThinBorderPen, new Point(cursor, y), new Point(cursor, y + 10d));
                cursor += segment;
            }
            dc.DrawLine(ThinBorderPen, new Point(panel.Right, y), new Point(panel.Right, y + 10d));
        }
        else
        {
            double x = panel.Left - DimensionLineOffset;
            dc.DrawLine(ThinBorderPen, new Point(x, panel.Top), new Point(x, panel.Bottom));
            double[] displayLengths = modelLengths.Reverse().ToArray();
            for (int displayIndex = 0; displayIndex < displayLengths.Length; displayIndex++)
            {
                double length = displayLengths[displayIndex];
                int sourceIndex = modelLengths.Count - 1 - displayIndex;
                double segment = axisLength * length / total;
                DrawDimensionBadge(dc, FormatModelLength(length), new Point(x - 47d, cursor + segment / 2d), axis, sourceIndex);
                dc.DrawLine(ThinBorderPen, new Point(x, cursor), new Point(x + 10d, cursor));
                cursor += segment;
            }
            dc.DrawLine(ThinBorderPen, new Point(x, panel.Bottom), new Point(x + 10d, panel.Bottom));
        }
    }

    private void DrawDimensionBadge(DrawingContext dc, string text, Point center, PanelDimensionAxis axis, int index)
    {
        var label = Formatted(text, 12.5d, MonoTypeface, Foreground, FontWeights.SemiBold);
        double badgeWidth = Math.Max(72d, label.Width + 12d);
        Rect badge = new(center.X - badgeWidth / 2d, center.Y - 12d, badgeWidth, 24d);
        Point lockCenter = axis == PanelDimensionAxis.Column
            ? new Point(center.X, center.Y + 27d)
            : new Point(center.X + 47d, center.Y);
        Rect lockRect = new(lockCenter.X - 10d, lockCenter.Y - 10d, 20d, 20d);
        _dimensionBadges[(axis, index)] = badge;
        _dimensionLocks[(axis, index)] = lockRect;
        dc.DrawText(label, new Point(center.X - label.Width / 2d, center.Y - label.Height / 2d));
        dc.DrawEllipse(Surface, null, lockCenter, 8.5d, 8.5d);
        DrawLock(dc, lockCenter, IsDimensionLocked(axis, index));
    }

    private Rect CellGroupRect(
        PanelCladdingCellGroup group,
        IReadOnlyList<double> xPositions,
        IReadOnlyList<double> yPositions)
    {
        int displayTop = _layout!.RowCount - 1 - group.MaximumRow;
        int displayBottom = _layout.RowCount - group.MinimumRow;
        double leftInset = group.MinimumColumn == 0 ? PanelFrameStroke : PanelDividerStroke / 2d;
        double rightInset = group.MaximumColumn == _layout.ColumnCount - 1 ? PanelFrameStroke : PanelDividerStroke / 2d;
        double topInset = group.MaximumRow == _layout.RowCount - 1 ? PanelFrameStroke : PanelDividerStroke / 2d;
        double bottomInset = group.MinimumRow == 0 ? PanelFrameStroke : PanelDividerStroke / 2d;
        double left = xPositions[group.MinimumColumn] + leftInset;
        double top = yPositions[displayTop] + topInset;
        return new Rect(
            left,
            top,
            Math.Max(1d, xPositions[group.MaximumColumn + 1] - rightInset - left),
            Math.Max(1d, yPositions[displayBottom] - bottomInset - top));
    }

    private Rect CellFragmentRect(
        PanelCladdingCellGroup group,
        PanelCladdingCell cell,
        IReadOnlyList<double> xPositions,
        IReadOnlyList<double> yPositions)
    {
        int displayRow = _layout!.RowCount - 1 - cell.Row;
        double leftInset = cell.Column == 0 ? PanelFrameStroke : PanelDividerStroke / 2d;
        if (cell.Column > 0 && group.Contains(cell.Column - 1, cell.Row))
        {
            leftInset = 0d;
        }
        double rightInset = cell.Column == _layout.ColumnCount - 1
            ? PanelFrameStroke
            : group.Contains(cell.Column + 1, cell.Row) ? 0d : PanelDividerStroke / 2d;
        double topInset = cell.Row == _layout.RowCount - 1
            ? PanelFrameStroke
            : group.Contains(cell.Column, cell.Row + 1) ? 0d : PanelDividerStroke / 2d;
        double bottomInset = cell.Row == 0
            ? PanelFrameStroke
            : group.Contains(cell.Column, cell.Row - 1) ? 0d : PanelDividerStroke / 2d;
        double left = xPositions[cell.Column] + leftInset;
        double top = yPositions[displayRow] + topInset;
        return new Rect(
            left,
            top,
            Math.Max(1d, xPositions[cell.Column + 1] - rightInset - left),
            Math.Max(1d, yPositions[displayRow + 1] - bottomInset - top));
    }

    private void DrawCell(DrawingContext dc, PanelCladdingCell cell, Rect rect, bool drawContent)
    {
        _values.TryGetValue(cell.UserTextKey, out string? rawValue);
        string value = rawValue?.Trim() ?? string.Empty;
        string? materialCode = ResolveMaterialCode(cell.UserTextKey, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        PanelCladdingMaterial? material = materialCode is not null && _materials.TryGetValue(materialCode, out PanelCladdingMaterial? found)
            ? found
            : null;
        bool selected = _view == PanelEditorView.Cladding && _selectedKeys.Contains(cell.UserTextKey);
        Brush fill = selected ? Selection : material?.Brush ?? Surface;
        if (_view == PanelEditorView.Extrusion)
        {
            fill = Mix(fill, Colors.White, 0.66d);
        }
        if (!selected && string.Equals(_hoverKey, cell.UserTextKey, StringComparison.OrdinalIgnoreCase))
        {
            fill = Mix(fill, Colors.Black, 0.055d);
        }
        dc.DrawRectangle(fill, null, rect);

        if (_view == PanelEditorView.Extrusion)
        {
            if (drawContent)
            {
                var ghostId = Formatted(cell.ShortLabel, 11d, MonoTypeface, Muted, FontWeights.SemiBold);
                dc.DrawText(ghostId, new Point(
                    rect.Left + (rect.Width - ghostId.Width) / 2d,
                    rect.Top + rect.Height / 2d - 17d));
                string displayMaterial = string.IsNullOrWhiteSpace(materialCode) ? "Unassigned" : materialCode;
                var ghostMaterial = Formatted(
                    displayMaterial,
                    11d,
                    BodyTypeface,
                    Muted,
                    string.IsNullOrWhiteSpace(materialCode) ? FontWeights.Normal : FontWeights.SemiBold);
                dc.PushClip(new RectangleGeometry(new Rect(
                    rect.Left + 6d,
                    rect.Top,
                    Math.Max(0d, rect.Width - 12d),
                    rect.Height)));
                dc.DrawText(ghostMaterial, new Point(
                    rect.Left + (rect.Width - ghostMaterial.Width) / 2d,
                    rect.Top + rect.Height / 2d + 2d));
                dc.Pop();
            }
            if (_placementAxis is not null && string.Equals(_hoverKey, cell.UserTextKey, StringComparison.OrdinalIgnoreCase))
            {
                var previewPen = new Pen(Selection, 3d) { DashStyle = new DashStyle([5d, 3d], 0d) };
                if (_placementAxis == PanelExtrusionAxis.Horizontal)
                {
                    dc.DrawLine(previewPen, new Point(rect.Left + 5d, rect.Top + rect.Height / 2d), new Point(rect.Right - 5d, rect.Top + rect.Height / 2d));
                }
                else
                {
                    dc.DrawLine(previewPen, new Point(rect.Left + rect.Width / 2d, rect.Top + 5d), new Point(rect.Left + rect.Width / 2d, rect.Bottom - 5d));
                }
            }
            return;
        }

        if (!drawContent)
        {
            return;
        }
        Brush textBrush = selected ? Foreground : material is null ? Foreground : ContrastBrush(material.Color);

        var idText = Formatted(cell.ShortLabel, 11d, MonoTypeface, textBrush, FontWeights.SemiBold);
        dc.DrawText(idText, new Point(rect.Left + (rect.Width - idText.Width) / 2d, rect.Top + rect.Height / 2d - 17d));
        if (IsCellReference(value, out string? reference))
        {
            DrawParentArrow(dc, rect, cell, reference!, textBrush);
        }
        else
        {
            string display = string.IsNullOrWhiteSpace(value) ? "Unassigned" : value;
            Brush valueBrush = string.IsNullOrWhiteSpace(value) ? Muted : textBrush;
            var valueText = Formatted(display, 11d, BodyTypeface, valueBrush, string.IsNullOrWhiteSpace(value) ? FontWeights.Normal : FontWeights.SemiBold);
            dc.PushClip(new RectangleGeometry(new Rect(rect.Left + 6d, rect.Top, Math.Max(0d, rect.Width - 12d), rect.Height)));
            dc.DrawText(valueText, new Point(rect.Left + (rect.Width - valueText.Width) / 2d, rect.Top + rect.Height / 2d + 2d));
            dc.Pop();
        }
    }

    private void DrawParentArrow(DrawingContext dc, Rect rect, PanelCladdingCell cell, string reference, Brush brush)
    {
        PanelCladdingCell? target = _layout?.Cells.FirstOrDefault(item =>
            string.Equals(item.ShortLabel, reference, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }
        Vector direction = new(target.Column - cell.Column, target.Row - cell.Row);
        if (direction.LengthSquared < 0.1d)
        {
            return;
        }
        direction.Normalize();
        Point center = new(rect.Left + rect.Width / 2d, rect.Top + rect.Height / 2d + 10d);
        Vector screen = new(direction.X, -direction.Y);
        Point start = center - screen * 12d;
        Point end = center + screen * 12d;
        var pen = new Pen(brush, 1.7d) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, start, end);
        Vector perpendicular = new(-screen.Y, screen.X);
        dc.DrawLine(pen, end, end - screen * 5d + perpendicular * 4d);
        dc.DrawLine(pen, end, end - screen * 5d - perpendicular * 4d);
    }

    private void DrawParentBoundaries(
        DrawingContext dc,
        PanelCladdingCellTopology topology,
        IReadOnlyList<double> xPositions,
        IReadOnlyList<double> yPositions)
    {
        if (_layout is null)
        {
            return;
        }
        var expanded = new PanelCladdingLogicalCellService().ExpandGroups(
            topology.Groups.Select(group => group.Cells).ToArray(), _values);
        var resolved = new PanelCladdingRegionService(new PanelCladdingKeyService()).Resolve(_layout.Cells, expanded);
        if (!resolved.Success || resolved.Data is null)
        {
            return;
        }
        var ownerByKey = resolved.Data.Regions.SelectMany(region => region.Cells.Select(cell =>
                (cell.UserTextKey, region.OwnerCellLabel)))
            .ToDictionary(item => item.UserTextKey, item => item.OwnerCellLabel, StringComparer.OrdinalIgnoreCase);
        var cellsByCoordinate = _layout.Cells.ToDictionary(cell => (cell.Column, cell.Row));
        var boundaries = new List<(Point Start, Point End, Brush GapBrush)>();
        foreach (PanelCladdingCell cell in _layout.Cells)
        {
            PanelCladdingCellGroup? group = topology.FindByCellKey(cell.UserTextKey);
            if (group is null || !ownerByKey.TryGetValue(cell.UserTextKey, out string? owner))
            {
                continue;
            }
            // Only physical neighbors share an edge. Representative labels can be arbitrarily far apart.
            foreach (bool vertical in new[] { true, false })
            {
                var coordinate = vertical ? (cell.Column + 1, cell.Row) : (cell.Column, cell.Row + 1);
                if (!cellsByCoordinate.TryGetValue(coordinate, out PanelCladdingCell? neighbor) ||
                    ReferenceEquals(group, topology.FindByCellKey(neighbor.UserTextKey)) ||
                    !ownerByKey.TryGetValue(neighbor.UserTextKey, out string? neighborOwner) ||
                    !string.Equals(owner, neighborOwner, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int displayRow = _layout.RowCount - 1 - cell.Row;
                Point start;
                Point end;
                if (vertical)
                {
                    double x = xPositions[cell.Column + 1];
                    start = new Point(x, yPositions[displayRow] +
                        (cell.Row == _layout.RowCount - 1 ? PanelFrameStroke : PanelDividerStroke / 2d));
                    end = new Point(x, yPositions[displayRow + 1] -
                        (cell.Row == 0 ? PanelFrameStroke : PanelDividerStroke / 2d));
                }
                else
                {
                    double y = yPositions[displayRow];
                    start = new Point(xPositions[cell.Column] +
                        (cell.Column == 0 ? PanelFrameStroke : PanelDividerStroke / 2d), y);
                    end = new Point(xPositions[cell.Column + 1] -
                        (cell.Column == _layout.ColumnCount - 1 ? PanelFrameStroke : PanelDividerStroke / 2d), y);
                }
                boundaries.Add((start, end, ResolveBoundaryGapBrush(group.Representative)));
            }
        }
        foreach ((Point start, Point end, Brush gapBrush) in boundaries)
        {
            var gapPen = new Pen(gapBrush, 1.5d)
            {
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            };
            dc.DrawLine(gapPen, start, end);
        }
        var dashPen = new Pen(GridLine, 1.5d)
        {
            DashStyle = new DashStyle([3d, 3d], 0d),
            DashCap = PenLineCap.Flat,
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat
        };
        foreach ((Point start, Point end, _) in boundaries)
        {
            dc.DrawLine(dashPen, start, end);
        }
    }

    private Brush ResolveBoundaryGapBrush(PanelCladdingCell child)
    {
        if (_selectedKeys.Contains(child.UserTextKey))
        {
            return Selection;
        }
        string? materialCode = ResolveMaterialCode(
            child.UserTextKey,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return materialCode is not null && _materials.TryGetValue(materialCode, out PanelCladdingMaterial? material)
            ? material.Brush
            : Surface;
    }

    private void DrawExtrusionLayout(
        DrawingContext dc,
        Rect panel,
        IReadOnlyList<PanelExtrusionSegment> segments)
    {
        if (_layout is null)
        {
            return;
        }
        var rendered = new List<(PanelExtrusionSegment Segment, Point Start, Point End, Point Midpoint)>();
        foreach (PanelExtrusionSegment segment in segments)
        {
            double x1;
            double x2;
            double y1;
            double y2;
            if (segment.Axis == PanelExtrusionAxis.Horizontal)
            {
                x1 = panel.Left + panel.Width * segment.Start / Math.Max(_layout.Width, 1e-9d);
                x2 = panel.Left + panel.Width * segment.End / Math.Max(_layout.Width, 1e-9d);
                y1 = y2 = panel.Bottom - panel.Height * segment.Offset / Math.Max(_layout.Height, 1e-9d);
            }
            else
            {
                x1 = x2 = panel.Left + panel.Width * segment.Offset / Math.Max(_layout.Width, 1e-9d);
                y1 = panel.Bottom - panel.Height * segment.Start / Math.Max(_layout.Height, 1e-9d);
                y2 = panel.Bottom - panel.Height * segment.End / Math.Max(_layout.Height, 1e-9d);
            }
            Point start = new(x1, y1);
            Point end = new(x2, y2);
            rendered.Add((segment, start, end, new Point((x1 + x2) / 2d, (y1 + y2) / 2d)));
        }

        dc.PushClip(new RectangleGeometry(panel));
        foreach ((PanelExtrusionSegment segment, Point start, Point end, _) in rendered.Where(item =>
                     _selectedExtrusions.Contains(item.Segment.Id)))
        {
            double curveWidth = segment.IsFrame ? PanelFrameStroke : PanelDividerStroke;
            double offset = (curveWidth + SelectionHaloBandWidth) / 2d + SelectionHaloGap;
            Vector normal = segment.Axis == PanelExtrusionAxis.Horizontal
                ? new Vector(0d, offset)
                : new Vector(offset, 0d);
            var haloPen = new Pen(Selection, SelectionHaloBandWidth)
            {
                StartLineCap = PenLineCap.Square,
                EndLineCap = PenLineCap.Square
            };
            dc.DrawLine(haloPen, start - normal, end - normal);
            dc.DrawLine(haloPen, start + normal, end + normal);
        }
        dc.Pop();

        foreach ((PanelExtrusionSegment segment, Point start, Point end, Point midpoint) in rendered)
        {
            bool hidden = !segment.IsFrame && segment.AtomicIds.Count > 0 &&
                segment.AtomicIds.All(_hiddenExtrusions.Contains);
            if (hidden)
            {
                var hiddenPen = new Pen(Hidden, 2.2d)
                {
                    DashStyle = new DashStyle([4d, 3d], 0d),
                    StartLineCap = PenLineCap.Flat,
                    EndLineCap = PenLineCap.Flat
                };
                dc.DrawLine(hiddenPen, start, end);
            }
            Rect hit = segment.Axis == PanelExtrusionAxis.Horizontal
                ? new Rect(Math.Min(start.X, end.X), start.Y - 9d, Math.Max(1d, Math.Abs(end.X - start.X)), 18d)
                : new Rect(start.X - 9d, Math.Min(start.Y, end.Y), 18d, Math.Max(1d, Math.Abs(end.Y - start.Y)));
            _segmentHitRects[segment.Id] = hit;

            string displayCode = hidden ? $"{segment.Code} · HIDDEN" : segment.Code;
            if (!hidden && _extrusionAssignments.TryGetValue(segment.Id, out IReadOnlyList<string>? assigned) &&
                assigned.Count > 0)
            {
                string primary = assigned[0].StartsWith("1D-", StringComparison.OrdinalIgnoreCase)
                    ? assigned[0][3..]
                    : assigned[0];
                displayCode += $" · {primary}" + (assigned.Count > 1 ? $" +{assigned.Count - 1}" : string.Empty);
            }
            var code = Formatted(displayCode, 11d, MonoTypeface, ExtrusionLabelText, FontWeights.SemiBold);
            double labelWidth = code.Width + 10d;
            double labelHeight = code.Height + 5d;
            Rect labelRect = new(
                midpoint.X - labelWidth / 2d,
                midpoint.Y - labelHeight / 2d,
                labelWidth,
                labelHeight);
            _segmentLabelRects[segment.Id] = labelRect;
            bool selected = _selectedExtrusions.Contains(segment.Id);
            dc.DrawRoundedRectangle(
                selected ? Selection : hidden ? HiddenSoft : ExtrusionLabel,
                new Pen(selected ? GridLine : hidden ? Hidden : Border, 1d),
                labelRect,
                4d,
                4d);
            dc.DrawText(code, new Point(labelRect.Left + 5d, labelRect.Top + 2d));
        }

        foreach (double vertical in _layout.VerticalOffsets)
        {
            foreach (double horizontal in _layout.HorizontalOffsets)
            {
                Point center = new(
                    panel.Left + panel.Width * vertical / Math.Max(_layout.Width, 1e-9d),
                    panel.Bottom - panel.Height * horizontal / Math.Max(_layout.Height, 1e-9d));
                dc.DrawEllipse(Surface, new Pen(GridLine, PanelDividerStroke), center, 3.4d, 3.4d);
            }
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _mouseDownPoint = e.GetPosition(this);
        _mouseDownModifiers = Keyboard.Modifiers;
        _isPointerDown = true;
        _isMarquee = false;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        Point point = e.GetPosition(this);
        if (_isPanning)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                _panOffset = _panStartOffset + (point - _panStartPoint);
                ClampPanOffset();
                InvalidateVisual();
            }
            else
            {
                EndPan();
            }
            e.Handled = true;
            return;
        }
        _hoverKey = HitTestTarget(point);
        if (_isPointerDown && e.LeftButton == MouseButtonState.Pressed)
        {
            Vector delta = point - _mouseDownPoint;
            if (_isMarquee || delta.Length > 6d)
            {
                _isMarquee = true;
                _marqueeRect = RectFromPoints(_mouseDownPoint, point);
            }
        }
        InvalidateVisual();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Point point = e.GetPosition(this);
        if (!_isMarquee && TryHandleDimensionClick(point))
        {
            _isPointerDown = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (!_isMarquee && _view == PanelEditorView.Extrusion && _placementAxis is not null)
        {
            string? cellKey = HitTestCell(point);
            if (!string.IsNullOrWhiteSpace(cellKey))
            {
                PanelExtrusionAxis axis = _placementAxis.Value;
                _isPointerDown = false;
                ReleaseMouseCapture();
                MullionPlacementRequested?.Invoke(this, new PanelMullionPlacementEventArgs(axis, cellKey));
                e.Handled = true;
                return;
            }
        }
        if (_isMarquee)
        {
            bool touchMode = point.X < _mouseDownPoint.X;
            IEnumerable<string> hits = _view == PanelEditorView.Extrusion
                ? _segmentHitRects
                    .Where(pair => touchMode ? pair.Value.IntersectsWith(_marqueeRect) : _marqueeRect.Contains(pair.Value))
                    .Select(pair => pair.Key)
                : _cellHitRects
                    .Where(item => touchMode ? item.Rect.IntersectsWith(_marqueeRect) : _marqueeRect.Contains(item.Rect))
                    .Select(item => item.Key);
            ApplySelection(hits, _mouseDownModifiers);
        }
        else
        {
            string? key = HitTestTarget(point);
            ApplySelection(key is null ? Array.Empty<string>() : [key], _mouseDownModifiers);
        }
        _isPointerDown = false;
        _isMarquee = false;
        ReleaseMouseCapture();
        InvalidateVisual();
        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_layout is null)
        {
            return;
        }
        Focus();
        _isPanning = true;
        _panStartPoint = e.GetPosition(this);
        _panStartOffset = _panOffset;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }
        EndPan();
        e.Handled = true;
    }

    private void EndPan()
    {
        _isPanning = false;
        Cursor = _placementAxis is null ? Cursors.Arrow : Cursors.Cross;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_layout is null || e.Delta == 0)
        {
            return;
        }
        double prior = Zoom;
        SetZoom(Zoom + Math.Sign(e.Delta) * ZoomStep);
        if (Math.Abs(Zoom - prior) > 1e-9d)
        {
            ZoomChanged?.Invoke(this, new PanelZoomChangedEventArgs(Zoom));
        }
        e.Handled = true;
    }

    private void ApplySelection(IEnumerable<string> keys, ModifierKeys modifiers)
    {
        string[] targets = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        HashSet<string> selection = _view == PanelEditorView.Extrusion ? _selectedExtrusions : _selectedKeys;
        if (targets.Length == 0)
        {
            selection.Clear();
        }
        else if (modifiers.HasFlag(ModifierKeys.Control))
        {
            foreach (string key in targets)
            {
                if (!selection.Add(key))
                {
                    selection.Remove(key);
                }
            }
        }
        else if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            selection.UnionWith(targets);
        }
        else
        {
            selection = targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        if (_view == PanelEditorView.Extrusion)
        {
            _selectedExtrusions = selection;
            ExtrusionSelectionChanged?.Invoke(this, _selectedExtrusions.ToArray());
        }
        else
        {
            _selectedKeys = selection;
            SelectionChanged?.Invoke(this, _selectedKeys.ToArray());
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (_view != PanelEditorView.Cladding)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        string? code = e.Data.GetData(DataFormats.Text) as string;
        _hoverKey = HitTestCell(e.GetPosition(this));
        e.Effects = !string.IsNullOrWhiteSpace(code) && _hoverKey is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (_view != PanelEditorView.Cladding)
        {
            e.Handled = true;
            return;
        }
        string? code = e.Data.GetData(DataFormats.Text) as string;
        string? key = HitTestCell(e.GetPosition(this));
        if (!string.IsNullOrWhiteSpace(code) && key is not null)
        {
            MaterialDropped?.Invoke(this, new PanelMaterialDropEventArgs(code, key));
        }
        _hoverKey = null;
        e.Handled = true;
        InvalidateVisual();
    }

    private string? HitTestCell(Point point) =>
        _cellHitRects.LastOrDefault(item => item.Rect.Contains(point)).Key;

    private string? HitTestTarget(Point point) =>
        _view == PanelEditorView.Extrusion && _placementAxis is null
            ? _segmentHitRects.LastOrDefault(pair => pair.Value.Contains(point)).Key
            : HitTestCell(point);

    private bool TryHandleDimensionClick(Point point)
    {
        if (_layout is null)
        {
            return false;
        }
        foreach (KeyValuePair<(PanelDimensionAxis Axis, int Index), Rect> pair in _dimensionLocks)
        {
            if (!pair.Value.Contains(point))
            {
                continue;
            }
            double value = DimensionValue(pair.Key.Axis, pair.Key.Index);
            DimensionLockToggleRequested?.Invoke(this, new PanelDimensionEditEventArgs(pair.Key.Axis, pair.Key.Index, value));
            return true;
        }
        foreach (KeyValuePair<(PanelDimensionAxis Axis, int Index), Rect> pair in _dimensionBadges)
        {
            if (!pair.Value.Contains(point))
            {
                continue;
            }
            double value = DimensionValue(pair.Key.Axis, pair.Key.Index);
            BeginInlineDimensionEdit(pair.Key.Axis, pair.Key.Index, value, pair.Value);
            return true;
        }
        return false;
    }

    private void BeginInlineDimensionEdit(PanelDimensionAxis axis, int index, double value, Rect badge)
    {
        CancelInlineDimensionEdit();
        _dimensionEditorAxis = axis;
        _dimensionEditorIndex = index;

        var textBox = new TextBox
        {
            Text = value.ToString("0.00000", CultureInfo.InvariantCulture),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Foreground,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0d),
            Padding = new Thickness(4d, 0d, 3d, 0d),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var editor = new Border
        {
            Width = badge.Width,
            Height = badge.Height,
            Background = Surface,
            BorderBrush = Accent,
            BorderThickness = new Thickness(1.5d),
            CornerRadius = new CornerRadius(5d),
            Child = textBox
        };
        SetLeft(editor, badge.Left);
        SetTop(editor, badge.Top);
        SetZIndex(editor, 20);
        Children.Add(editor);
        _dimensionEditor = editor;
        _dimensionEditorTextBox = textBox;

        textBox.PreviewKeyDown += OnDimensionEditorKeyDown;
        textBox.LostKeyboardFocus += OnDimensionEditorLostKeyboardFocus;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_dimensionEditorTextBox != textBox)
            {
                return;
            }
            textBox.Focus();
            textBox.SelectAll();
        });
    }

    private void OnDimensionEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInlineDimensionEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelInlineDimensionEdit();
            Focus();
            e.Handled = true;
        }
    }

    private void OnDimensionEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_dimensionEditorClosing)
        {
            CommitInlineDimensionEdit();
        }
    }

    private void CommitInlineDimensionEdit()
    {
        if (_dimensionEditorClosing || _dimensionEditorTextBox is null || _dimensionEditor is null)
        {
            return;
        }
        string text = _dimensionEditorTextBox.Text.Trim();
        bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        if (!parsed || !double.IsFinite(value))
        {
            _dimensionEditor.BorderBrush = Error;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                _dimensionEditorTextBox?.Focus();
                _dimensionEditorTextBox?.SelectAll();
            });
            return;
        }

        PanelDimensionAxis axis = _dimensionEditorAxis;
        int index = _dimensionEditorIndex;
        CloseInlineDimensionEdit();
        DimensionValueCommitted?.Invoke(this, new PanelDimensionEditEventArgs(axis, index, value));
    }

    private void CancelInlineDimensionEdit()
    {
        if (_dimensionEditor is not null)
        {
            CloseInlineDimensionEdit();
        }
    }

    private void CloseInlineDimensionEdit()
    {
        _dimensionEditorClosing = true;
        if (_dimensionEditorTextBox is not null)
        {
            _dimensionEditorTextBox.PreviewKeyDown -= OnDimensionEditorKeyDown;
            _dimensionEditorTextBox.LostKeyboardFocus -= OnDimensionEditorLostKeyboardFocus;
        }
        if (_dimensionEditor is not null)
        {
            Children.Remove(_dimensionEditor);
        }
        _dimensionEditorTextBox = null;
        _dimensionEditor = null;
        _dimensionEditorClosing = false;
    }

    private double DimensionValue(PanelDimensionAxis axis, int index)
    {
        if (_layout is null)
        {
            return 0d;
        }
        IReadOnlyList<double> values = axis == PanelDimensionAxis.Column
            ? SegmentLengths(_layout.Width, _layout.VerticalOffsets)
            : SegmentLengths(_layout.Height, _layout.HorizontalOffsets);
        return index >= 0 && index < values.Count ? values[index] : 0d;
    }

    private string? ResolveMaterialCode(string key, HashSet<string> visited)
    {
        if (!visited.Add(key) || !_values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!IsCellReference(value, out string? reference))
        {
            return value.Trim();
        }
        PanelCladdingCell? parent = _layout?.Cells.FirstOrDefault(item =>
            string.Equals(item.ShortLabel, reference, StringComparison.OrdinalIgnoreCase));
        return parent is null ? null : ResolveMaterialCode(parent.UserTextKey, visited);
    }

    private bool IsCellReference(string? value, out string? normalized)
    {
        normalized = null;
        if (_layout is null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        string candidate = value.Trim().ToUpperInvariant();
        if (_layout.Cells.Any(cell => string.Equals(cell.ShortLabel, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            normalized = candidate;
            return true;
        }
        return false;
    }

    private static string FormatModelLength(double value) => value.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);

    private bool IsDimensionLocked(PanelDimensionAxis axis, int index)
    {
        IReadOnlyList<bool> locks = axis == PanelDimensionAxis.Column ? _columnLocks : _rowLocks;
        return index >= 0 && index < locks.Count && locks[index];
    }

    public static string UnitLabel(double millimetersPerModelUnit)
    {
        if (Math.Abs(millimetersPerModelUnit - 25.4d) < 1e-6d)
        {
            return "in";
        }
        if (Math.Abs(millimetersPerModelUnit - 304.8d) < 1e-6d)
        {
            return "ft";
        }
        if (Math.Abs(millimetersPerModelUnit - 1000d) < 1e-6d)
        {
            return "m";
        }
        if (Math.Abs(millimetersPerModelUnit - 10d) < 1e-6d)
        {
            return "cm";
        }
        return "mm";
    }

    private static IReadOnlyList<double> SegmentLengths(double total, IReadOnlyList<double> offsets)
    {
        double[] positions = [0d, .. offsets.OrderBy(value => value), total];
        return positions.Zip(positions.Skip(1), (start, end) => Math.Max(1e-9d, end - start)).ToArray();
    }

    private static double[] BuildPositions(double start, double length, IReadOnlyList<double> modelLengths)
    {
        double total = Math.Max(1e-9d, modelLengths.Sum());
        var positions = new double[modelLengths.Count + 1];
        positions[0] = start;
        for (int index = 0; index < modelLengths.Count; index++)
        {
            positions[index + 1] = positions[index] + length * modelLengths[index] / total;
        }
        return positions;
    }

    private static Rect RectFromPoints(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static void DrawLock(DrawingContext dc, Point center, bool locked)
    {
        Brush brush = locked ? Foreground : Muted;
        var pen = new Pen(brush, 1d);
        dc.DrawRoundedRectangle(null, pen, new Rect(center.X - 4d, center.Y - 1d, 8d, 7d), 1d, 1d);
        if (locked)
        {
            dc.DrawArc(pen, new Point(center.X - 3d, center.Y - 1d), new Point(center.X + 3d, center.Y - 1d), new Size(3d, 4d), 180d, false, SweepDirection.Clockwise);
        }
        else
        {
            dc.DrawArc(pen, new Point(center.X - 3d, center.Y - 1d), new Point(center.X + 1d, center.Y - 4d), new Size(3d, 4d), 180d, false, SweepDirection.Clockwise);
        }
    }

    private static void DrawText(DrawingContext dc, string text, Brush brush, double size, Point origin, Typeface typeface) =>
        dc.DrawText(Formatted(text, size, typeface, brush, FontWeights.Normal), origin);

    private static FormattedText Formatted(string text, double size, Typeface typeface, Brush brush, FontWeight weight)
    {
        Typeface resolved = weight == FontWeights.Normal
            ? typeface
            : new Typeface(typeface.FontFamily, typeface.Style, weight, typeface.Stretch);
        return new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, resolved, size, brush, 1d)
        {
            Trimming = TextTrimming.CharacterEllipsis,
            MaxLineCount = 1
        };
    }

    private static Brush ContrastBrush(Color color)
    {
        double luminance = (0.2126d * color.R + 0.7152d * color.G + 0.0722d * color.B) / 255d;
        return luminance < 0.53d ? Brushes.White : Foreground;
    }

    private static Brush Mix(Brush source, Color overlay, double amount)
    {
        Color baseColor = source is SolidColorBrush solid ? solid.Color : Colors.White;
        byte MixChannel(byte from, byte to) => (byte)Math.Clamp(from + (to - from) * amount, 0d, 255d);
        return FrozenBrush(Color.FromRgb(MixChannel(baseColor.R, overlay.R), MixChannel(baseColor.G, overlay.G), MixChannel(baseColor.B, overlay.B)));
    }

    private static SolidColorBrush FrozenBrush(string hex) => FrozenBrush(PanelCladdingMaterialPalette.ParseColor(hex));

    private static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}

public sealed class PanelMaterialDropEventArgs(string materialCode, string cellKey) : EventArgs
{
    public string MaterialCode { get; } = materialCode;
    public string CellKey { get; } = cellKey;
}

public enum PanelDimensionAxis
{
    Column,
    Row
}

public sealed class PanelDimensionEditEventArgs(PanelDimensionAxis axis, int index, double value) : EventArgs
{
    public PanelDimensionAxis Axis { get; } = axis;
    public int Index { get; } = index;
    public double Value { get; } = value;
}

public sealed class PanelMullionPlacementEventArgs(PanelExtrusionAxis axis, string cellKey) : EventArgs
{
    public PanelExtrusionAxis Axis { get; } = axis;
    public string CellKey { get; } = cellKey;
}

public sealed class PanelZoomChangedEventArgs(double zoom) : EventArgs
{
    public double Zoom { get; } = zoom;
}

internal static class DrawingContextExtensions
{
    public static void DrawArc(
        this DrawingContext context,
        Pen pen,
        Point start,
        Point end,
        Size radius,
        double rotationAngle,
        bool isLargeArc,
        SweepDirection sweepDirection)
    {
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, radius, rotationAngle, isLargeArc, sweepDirection, true));
        var geometry = new PathGeometry([figure]);
        context.DrawGeometry(null, pen, geometry);
    }
}
