using Eto.Drawing;
using Eto.Forms;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public sealed class PanelCladdingPreviewCanvas : Drawable
{
    private readonly Font _labelFont = new("Segoe UI", 9f, FontStyle.Bold, FontDecoration.None);
    private PanelCladdingLayout? _layout;
    private PanelProjectedScene? _scene;
    private IReadOnlyDictionary<string, string> _values = new Dictionary<string, string>();
    private string _selectedKey = string.Empty;

    public PanelCladdingPreviewCanvas()
    {
        BackgroundColor = Color.FromArgb(255, 36, 43, 49);
        Paint += OnPaint;
        MouseDown += OnMouseDown;
    }

    public event EventHandler<string>? SelectedCellChanged;

    public void SetScene(
        PanelCladdingLayout layout,
        PanelProjectedScene scene,
        IReadOnlyDictionary<string, string> values)
    {
        _layout = layout;
        _scene = scene;
        _values = values;
        _selectedKey = layout.Cells.FirstOrDefault()?.UserTextKey ?? string.Empty;
        Invalidate();
    }

    public void SetValues(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
        Invalidate();
    }

    public void SelectCell(string key)
    {
        _selectedKey = key;
        Invalidate();
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(BackgroundColor);
        if (_layout is null || _scene is null)
        {
            e.Graphics.DrawText(_labelFont, Colors.LightSlateGray, 18f, 18f, "Select a panel to preview.");
            return;
        }

        CanvasTransform transform = CreateTransform(_scene, Width, Height);
        using var edgePen = new Pen(Color.FromArgb(255, 75, 89, 98), 1f);
        foreach (PanelProjectedTriangle triangle in _scene.Triangles)
        {
            PointF[] points = triangle.Points.Select(transform.Map).ToArray();
            Color fill = Color.FromArgb(255, triangle.Shade, triangle.Shade, Math.Min(255, triangle.Shade + 8));
            e.Graphics.FillPolygon(fill, points);
            e.Graphics.DrawPolygon(edgePen, points);
        }

        using var gridPen = new Pen(Color.FromArgb(255, 22, 190, 96), 2f);
        foreach (IReadOnlyList<PanelPoint2> polyline in _scene.GridPolylines)
        {
            if (polyline.Count >= 2)
            {
                e.Graphics.DrawLines(gridPen, polyline.Select(transform.Map));
            }
        }

        foreach (PanelProjectedCell cell in _scene.Cells)
        {
            bool selected = string.Equals(cell.UserTextKey, _selectedKey, StringComparison.OrdinalIgnoreCase);
            PointF[] boundary = cell.Boundary.Select(transform.Map).ToArray();
            if (selected && boundary.Length >= 3)
            {
                e.Graphics.FillPolygon(Color.FromArgb(70, 255, 170, 35), boundary);
                using var selectedPen = new Pen(Color.FromArgb(255, 255, 170, 35), 3f);
                e.Graphics.DrawPolygon(selectedPen, boundary);
            }

            PanelCladdingCell? logical = _layout.Cells.FirstOrDefault(item =>
                string.Equals(item.UserTextKey, cell.UserTextKey, StringComparison.OrdinalIgnoreCase));
            _values.TryGetValue(cell.UserTextKey, out string? value);
            string text = string.IsNullOrWhiteSpace(value) ? logical?.ShortLabel ?? string.Empty : value;
            PointF center = transform.Map(cell.Center);
            SizeF size = e.Graphics.MeasureString(_labelFont, text);
            e.Graphics.DrawText(
                _labelFont,
                selected ? Color.FromArgb(255, 255, 185, 70) : Colors.DarkOrange,
                center.X - size.Width / 2f,
                center.Y - size.Height / 2f,
                text);
        }

        string title = $"{_layout.GeometryClass}  |  {_layout.ColumnCount} x {_layout.RowCount}";
        e.Graphics.DrawText(_labelFont, Colors.WhiteSmoke, 12f, 10f, title);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_scene is null)
        {
            return;
        }
        CanvasTransform transform = CreateTransform(_scene, Width, Height);
        PanelCladdingPreviewHit? best = null;
        foreach (PanelProjectedCell cell in _scene.Cells)
        {
            PointF[] polygon = cell.Boundary.Select(transform.Map).ToArray();
            if (polygon.Length >= 3 && Contains(polygon, e.Location))
            {
                best = new PanelCladdingPreviewHit(cell.UserTextKey, 0d);
                break;
            }
            PointF center = transform.Map(cell.Center);
            double distance = Math.Pow(center.X - e.Location.X, 2d) + Math.Pow(center.Y - e.Location.Y, 2d);
            if (best is null || distance < best.Value.DistanceSquared)
            {
                best = new PanelCladdingPreviewHit(cell.UserTextKey, distance);
            }
        }
        if (best is null)
        {
            return;
        }
        _selectedKey = best.Value.Key;
        SelectedCellChanged?.Invoke(this, _selectedKey);
        Invalidate();
    }

    private static bool Contains(IReadOnlyList<PointF> polygon, PointF point)
    {
        bool inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            PointF a = polygon[current];
            PointF b = polygon[previous];
            bool crosses = (a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / Math.Max(1e-9f, b.Y - a.Y) + a.X;
            if (crosses)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static CanvasTransform CreateTransform(PanelProjectedScene scene, int width, int height)
    {
        double rangeX = Math.Max(1e-9d, scene.MaxX - scene.MinX);
        double rangeY = Math.Max(1e-9d, scene.MaxY - scene.MinY);
        double margin = 24d;
        double top = 42d;
        double usableWidth = Math.Max(1d, width - 2d * margin);
        double usableHeight = Math.Max(1d, height - top - margin);
        double scale = Math.Min(usableWidth / rangeX, usableHeight / rangeY);
        double offsetX = margin + (usableWidth - rangeX * scale) / 2d - scene.MinX * scale;
        double offsetY = top + (usableHeight - rangeY * scale) / 2d + scene.MaxY * scale;
        return new CanvasTransform(scale, offsetX, offsetY);
    }

    private readonly record struct PanelCladdingPreviewHit(string Key, double DistanceSquared);

    private readonly record struct CanvasTransform(double Scale, double OffsetX, double OffsetY)
    {
        public PointF Map(PanelPoint2 point) => new(
            (float)(OffsetX + point.X * Scale),
            (float)(OffsetY - point.Y * Scale));
    }
}

