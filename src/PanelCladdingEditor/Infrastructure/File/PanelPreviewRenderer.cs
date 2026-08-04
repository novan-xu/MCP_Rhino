using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Infrastructure.PanelCladding;

public sealed class PanelPreviewRenderer : IPanelPreviewRenderer
{
    private const double YawRadians = -35d * Math.PI / 180d;
    private const double PitchRadians = 24d * Math.PI / 180d;

    public PanelProjectedScene Project(PanelPreviewGeometry preview)
    {
        var projectedVertices = preview.Vertices.Select(ProjectPoint).ToArray();
        var triangles = new List<PanelProjectedTriangle>(preview.Triangles.Count);
        foreach (PanelTriangle triangle in preview.Triangles)
        {
            PanelPoint3 a = preview.Vertices[triangle.A];
            PanelPoint3 b = preview.Vertices[triangle.B];
            PanelPoint3 c = preview.Vertices[triangle.C];
            PanelPoint3 normal = Cross(Subtract(b, a), Subtract(c, a));
            double length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
            double light = length <= 1e-12d
                ? 0.5d
                : Math.Abs((normal.X * -0.35d + normal.Y * 0.45d + normal.Z * 0.82d) / length);
            byte shade = (byte)Math.Clamp(105d + light * 105d, 85d, 225d);
            ProjectedPoint pa = projectedVertices[triangle.A];
            ProjectedPoint pb = projectedVertices[triangle.B];
            ProjectedPoint pc = projectedVertices[triangle.C];
            triangles.Add(new PanelProjectedTriangle
            {
                Points = new[] { pa.Point, pb.Point, pc.Point },
                Depth = (pa.Depth + pb.Depth + pc.Depth) / 3d,
                Shade = shade
            });
        }

        triangles.Sort((left, right) => left.Depth.CompareTo(right.Depth));
        var grid = preview.GridPolylines
            .Select(polyline => (IReadOnlyList<PanelPoint2>)polyline.Select(point => ProjectPoint(point).Point).ToArray())
            .ToArray();
        var cells = preview.Cells.Select(cell => new PanelProjectedCell
        {
            UserTextKey = cell.UserTextKey,
            Center = ProjectPoint(cell.Center).Point,
            Boundary = cell.Boundary.Select(point => ProjectPoint(point).Point).ToArray()
        }).ToArray();

        var allPoints = triangles.SelectMany(item => item.Points)
            .Concat(grid.SelectMany(item => item))
            .ToArray();
        double minX = allPoints.Length == 0 ? 0d : allPoints.Min(point => point.X);
        double maxX = allPoints.Length == 0 ? 1d : allPoints.Max(point => point.X);
        double minY = allPoints.Length == 0 ? 0d : allPoints.Min(point => point.Y);
        double maxY = allPoints.Length == 0 ? 1d : allPoints.Max(point => point.Y);
        return new PanelProjectedScene
        {
            Triangles = triangles,
            GridPolylines = grid,
            Cells = cells,
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY
        };
    }

    public OperationResponse<byte[]> RenderPng(PanelCladdingLayout layout, int width, int height)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResponse<byte[]>.Fail("PANEL_CLADDING_PREVIEW_WINDOWS_REQUIRED");
        }

        if (width is < 240 or > 2400 || height is < 180 or > 1800)
        {
            return OperationResponse<byte[]>.Fail("PANEL_CLADDING_PREVIEW_SIZE_INVALID");
        }

        try
        {
            return OperationResponse<byte[]>.Ok(RenderPngWindows(layout, Project(layout.Preview), width, height));
        }
        catch (Exception ex)
        {
            return OperationResponse<byte[]>.Fail($"PANEL_CLADDING_PREVIEW_RENDER_FAILED: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] RenderPngWindows(PanelCladdingLayout layout, PanelProjectedScene scene, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.FromArgb(36, 43, 49));

        const float titleHeight = 42f;
        SceneTransform transform = CreateTransform(scene, width, height, 24f, titleHeight + 12f);
        using var edgePen = new Pen(Color.FromArgb(70, 85, 96), 1f);
        foreach (PanelProjectedTriangle triangle in scene.Triangles)
        {
            PointF[] points = triangle.Points.Select(transform.Map).ToArray();
            using var brush = new SolidBrush(Color.FromArgb(triangle.Shade, triangle.Shade, Math.Min(255, triangle.Shade + 8)));
            graphics.FillPolygon(brush, points);
            graphics.DrawPolygon(edgePen, points);
        }

        using var gridPen = new Pen(Color.FromArgb(25, 190, 100), 2.2f);
        foreach (IReadOnlyList<PanelPoint2> polyline in scene.GridPolylines)
        {
            if (polyline.Count >= 2)
            {
                graphics.DrawLines(gridPen, polyline.Select(transform.Map).ToArray());
            }
        }

        using var cellFont = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        using var cellBrush = new SolidBrush(Color.FromArgb(255, 168, 35));
        foreach (PanelProjectedCell cell in scene.Cells)
        {
            PanelCladdingCell? logical = layout.Cells.FirstOrDefault(item => item.UserTextKey == cell.UserTextKey);
            string text = logical?.ShortLabel ?? string.Empty;
            PointF point = transform.Map(cell.Center);
            SizeF size = graphics.MeasureString(text, cellFont);
            graphics.DrawString(text, cellFont, cellBrush, point.X - size.Width / 2f, point.Y - size.Height / 2f);
        }

        using var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
        using var titleBrush = new SolidBrush(Color.WhiteSmoke);
        string title = $"{layout.SystemCode}  |  {layout.GeometryClass}  |  {layout.ColumnCount} x {layout.RowCount}";
        graphics.DrawString(title, titleFont, titleBrush, 18f, 12f);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    public static SceneTransform CreateTransform(
        PanelProjectedScene scene,
        double width,
        double height,
        double horizontalMargin,
        double topMargin)
    {
        double rangeX = Math.Max(1e-9d, scene.MaxX - scene.MinX);
        double rangeY = Math.Max(1e-9d, scene.MaxY - scene.MinY);
        double usableWidth = Math.Max(1d, width - 2d * horizontalMargin);
        double usableHeight = Math.Max(1d, height - topMargin - horizontalMargin);
        double scale = Math.Min(usableWidth / rangeX, usableHeight / rangeY);
        double offsetX = horizontalMargin + (usableWidth - rangeX * scale) / 2d - scene.MinX * scale;
        double offsetY = topMargin + (usableHeight - rangeY * scale) / 2d + scene.MaxY * scale;
        return new SceneTransform(scale, offsetX, offsetY);
    }

    private static ProjectedPoint ProjectPoint(PanelPoint3 point)
    {
        double yawX = Math.Cos(YawRadians) * point.X - Math.Sin(YawRadians) * point.Z;
        double yawZ = Math.Sin(YawRadians) * point.X + Math.Cos(YawRadians) * point.Z;
        double screenY = Math.Cos(PitchRadians) * point.Y - Math.Sin(PitchRadians) * yawZ;
        double depth = Math.Sin(PitchRadians) * point.Y + Math.Cos(PitchRadians) * yawZ;
        return new ProjectedPoint(new PanelPoint2(yawX, screenY), depth);
    }

    private static PanelPoint3 Subtract(PanelPoint3 a, PanelPoint3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static PanelPoint3 Cross(PanelPoint3 a, PanelPoint3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);

    private readonly record struct ProjectedPoint(PanelPoint2 Point, double Depth);

    public readonly record struct SceneTransform(double Scale, double OffsetX, double OffsetY)
    {
        public PointF Map(PanelPoint2 point) => new(
            (float)(OffsetX + point.X * Scale),
            (float)(OffsetY - point.Y * Scale));

        public PanelPoint2 MapToPanel(PanelPoint2 screen) => new(
            (screen.X - OffsetX) / Scale,
            (OffsetY - screen.Y) / Scale);
    }
}

