using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

// Standalone equivalent of MCP_Rhino's gravity-aware standard four-point skill.
public sealed class PanelCladdingPointOrderService
{
    public OperationResponse<IReadOnlyList<PanelPoint3>> Order(
        IReadOnlyList<PanelPoint3> corners, PanelPoint3 frontNormal, double tolerance)
    {
        if (corners.Count != 4 || !double.IsFinite(tolerance) || tolerance <= 0 ||
            corners.Any(p => !Finite(p)) || !Finite(frontNormal)) return Fail("INVALID_QUAD_INPUT");
        double length = Math.Sqrt(Dot(frontNormal, frontNormal));
        if (!double.IsFinite(length) || length <= 1e-12) return Fail("DEGENERATE_SURFACE_NORMAL");
        var normal = Scale(frontNormal, 1 / length);
        var down = new PanelPoint3(normal.Z * normal.X, normal.Z * normal.Y, normal.Z * normal.Z - 1);
        length = Math.Sqrt(Dot(down, down));
        if (length <= 1e-12) return Fail("SURFACE_GRAVITY_PROJECTION_DEGENERATE");
        down = Scale(down, 1 / length);
        var up = Scale(down, -1);
        var right = Cross(normal, down);
        // Work relative to a nearby point to avoid loss of precision at large coordinates.
        var offsets = corners.Select(p => Subtract(p, corners[0])).ToArray();
        var center = new PanelPoint3(offsets.Average(p => p.X), offsets.Average(p => p.Y), offsets.Average(p => p.Z));
        if (offsets.Any(p => Math.Abs(Dot(p, normal)) > tolerance)) return Fail("NONPLANAR_QUAD");
        var projected = offsets.Select((p, i) => (Index: i, X: Dot(Subtract(p, center), right), Y: Dot(Subtract(p, center), up))).ToArray();
        double minX = projected.Min(p => p.X), minY = projected.Min(p => p.Y);
        double width = projected.Max(p => p.X) - minX, height = projected.Max(p => p.Y) - minY;
        if (width <= tolerance || height <= tolerance) return Fail("SURFACE_LOCAL_BOUNDARY_DEGENERATE");
        var anchors = projected.Select(p => (p.Index, Score: Math.Sqrt(Math.Pow((p.X - minX) / width, 2) + Math.Pow((p.Y - minY) / height, 2))))
            .OrderBy(p => p.Score).ToArray();
        if (anchors[1].Score - anchors[0].Score <= 1e-9) return Fail("REFERENCE_CURVE_AMBIGUOUS");
        var ordered = projected.OrderByDescending(p => Math.Atan2(p.Y, p.X)).ToArray();
        int start = Array.FindIndex(ordered, p => p.Index == anchors[0].Index);
        ordered = ordered.Skip(start).Concat(ordered.Take(start)).ToArray();
        for (int i = 0; i < 4; i++)
        {
            var a = ordered[i]; var b = ordered[(i + 1) % 4]; var c = ordered[(i + 2) % 4];
            double edgeLength = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            double turn = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (edgeLength <= tolerance || turn >= -tolerance * edgeLength) return Fail("NONCONVEX_OR_DEGENERATE_QUAD");
        }
        return OperationResponse<IReadOnlyList<PanelPoint3>>.Ok(ordered.Select(p => corners[p.Index]).ToArray());
    }

    private static bool Finite(PanelPoint3 p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
    private static double Dot(PanelPoint3 a, PanelPoint3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static PanelPoint3 Scale(PanelPoint3 p, double s) => new(p.X * s, p.Y * s, p.Z * s);
    private static PanelPoint3 Subtract(PanelPoint3 a, PanelPoint3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static PanelPoint3 Cross(PanelPoint3 a, PanelPoint3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static OperationResponse<IReadOnlyList<PanelPoint3>> Fail(string reason) => OperationResponse<IReadOnlyList<PanelPoint3>>.Fail(reason);
}
