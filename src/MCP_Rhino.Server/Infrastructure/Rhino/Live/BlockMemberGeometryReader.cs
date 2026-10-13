extern alias rhinocommon;

using MCP_Rhino.Server.Contracts.Responses;
using rhinocommon::Rhino.Geometry;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

internal static class BlockMemberGeometryReader
{
    internal static BlockMemberGeometryResponse Read(GeometryBase source, Transform world, double tolerance)
    {
        var result = new BlockMemberGeometryResponse { IsSurface = source is Brep or Surface };
        using GeometryBase geometry = source.Duplicate();
        if (!geometry.Transform(world) || !geometry.IsValid)
        {
            result.Reason = "Invalid geometry or failed occurrence transform.";
            return result;
        }
        // Own the Brep conversion, including when the input was already a Brep.
        using Brep? brep = geometry switch
        {
            Brep b => b.DuplicateBrep(),
            Extrusion e => e.ToBrep(),
            Surface s => s.ToBrep(),
            _ => null
        };
        if (brep is null)
        {
            result.Reason = "Leaf is not a surface, Brep or extrusion.";
            return result;
        }
        result.IsSurface = true;
        result.FaceCount = brep.Faces.Count;
        result.LoopCount = brep.Loops.Count;
        using AreaMassProperties? area = AreaMassProperties.Compute(brep);
        if (area is not null && double.IsFinite(area.Area)) result.Area = area.Area;
        if (brep.Faces.Count != 1 || brep.Loops.Count != 1 || !brep.Faces[0].IsPlanar(tolerance))
        {
            result.Reason = "Requires one planar face with one boundary loop (no holes).";
            return result;
        }
        using Curve? boundary = brep.Loops[0].To3dCurve();
        Curve[] edges = boundary?.DuplicateSegments() ?? Array.Empty<Curve>();
        try
        {
            if (edges.Length != 4 || edges.Any(e => !e.IsLinear(tolerance)))
            {
                result.Reason = "Boundary is not a four-straight-edge quad.";
                return result;
            }
            Point3d[] corners = edges.Select(e => e.PointAtStart).ToArray();
            for (int i = 0; i < 4; i++)
            {
                if (edges[i].PointAtEnd.DistanceTo(corners[(i + 1) % 4]) > tolerance
                    || corners[i].DistanceTo(corners[(i + 1) % 4]) <= tolerance)
                {
                    result.Reason = "Boundary is disconnected or degenerate.";
                    return result;
                }
            }
            if (result.Area is null || result.Area <= tolerance * tolerance)
            {
                result.Reason = "Surface area is unavailable or degenerate.";
                return result;
            }
            result.Corners = corners.Select(p => new[] { p.X, p.Y, p.Z }).ToArray();
            result.EdgeLengths = Enumerable.Range(0, 4).Select(i => corners[i].DistanceTo(corners[(i + 1) % 4])).ToArray();
            result.DiagonalLengths = new[] { corners[0].DistanceTo(corners[2]), corners[1].DistanceTo(corners[3]) };
            result.Perimeter = result.EdgeLengths.Sum();
            result.Status = "PlanarQuad";
            result.Reason = "Ordered boundary corners; linearity and planarity checked at model absolute tolerance.";
            return result;
        }
        finally { foreach (Curve edge in edges) edge.Dispose(); }
    }
}
