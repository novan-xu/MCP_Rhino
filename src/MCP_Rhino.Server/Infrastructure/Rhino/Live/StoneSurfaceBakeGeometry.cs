extern alias rhinocommon;

using Brep = rhinocommon::Rhino.Geometry.Brep;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Line = rhinocommon::Rhino.Geometry.Line;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoMath = rhinocommon::Rhino.RhinoMath;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using UnitSystem = rhinocommon::Rhino.UnitSystem;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

internal static class StoneSurfaceBakeGeometry
{
    internal static double HeightInModelUnits(double inches, UnitSystem units)
    {
        if (!double.IsFinite(inches) || inches <= 0
            || !Enum.IsDefined(units) || units is UnitSystem.None or UnitSystem.Unset or UnitSystem.CustomUnits)
            throw new ArgumentException("Surface height requires a positive finite inch value and standard document units.");
        double height = units == UnitSystem.Inches ? inches : inches * RhinoMath.UnitScale(UnitSystem.Inches, units);
        if (!double.IsFinite(height) || height <= 0)
            throw new ArgumentException("Surface height could not be converted to document units.");
        return height;
    }

    internal static NurbsSurface Create(double length, double height)
    {
        return NurbsSurface.CreateFromCorners(Point3d.Origin, new Point3d(length, 0, 0),
            new Point3d(length, 0, height), new Point3d(0, 0, height))
            ?? throw new InvalidOperationException("Unable to construct the stone surface.");
    }

    internal static bool TryPlacement(Line source, Transform sourceTransform, double tolerance,
        out Transform placement)
    {
        placement = Transform.Identity;
        if (!source.IsValid || source.Length <= tolerance || !sourceTransform.IsValid
            || sourceTransform.M30 != 0 || sourceTransform.M31 != 0 || sourceTransform.M32 != 0 || sourceTransform.M33 != 1)
            return false;
        Point3d start = source.From;
        Point3d end = source.To;
        start.Transform(sourceTransform);
        end.Transform(sourceTransform);
        if (!start.IsValid || !end.IsValid) return false;
        double dx = end.X - start.X, dy = end.Y - start.Y, dz = end.Z - start.Z;
        double horizontalLength = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(horizontalLength) || horizontalLength <= tolerance) return false;

        // X maps the canonical bottom edge exactly, including mirror/scale/tilt.
        // Z is unit world +Z so parent transformations cannot change the height.
        placement.M00 = dx / source.Length; placement.M10 = dy / source.Length; placement.M20 = dz / source.Length;
        placement.M01 = -dy / horizontalLength; placement.M11 = dx / horizontalLength; placement.M21 = 0;
        placement.M02 = 0; placement.M12 = 0; placement.M22 = 1;
        placement.M03 = start.X; placement.M13 = start.Y; placement.M23 = start.Z;
        return placement.IsValid;
    }

    internal static bool IsCompatible(GeometryBase geometry, double length, double height, double tolerance)
    {
        Surface? surface = geometry as Surface;
        if (geometry is Brep brep)
        {
            if (brep.Faces.Count != 1 || !brep.Faces[0].IsSurface) return false;
            surface = brep.Faces[0];
        }
        if (surface is null || !surface.IsValid) return false;
        using NurbsSurface nurbs = surface.ToNurbsSurface();
        if (nurbs is null || nurbs.OrderU != 2 || nurbs.OrderV != 2
            || nurbs.Points.CountU != 2 || nurbs.Points.CountV != 2) return false;
        Point3d[] expected = { Point3d.Origin, new(length, 0, 0), new(length, 0, height), new(0, 0, height) };
        var corners = new List<Point3d>();
        for (int u = 0; u < 2; u++)
        for (int v = 0; v < 2; v++)
        {
            var point = nurbs.Points.GetControlPoint(u, v);
            if (Math.Abs(point.Weight - 1) > 1e-12) return false;
            corners.Add(point.Location);
        }
        foreach (Point3d point in expected)
        {
            int match = corners.FindIndex(candidate => candidate.DistanceTo(point) <= tolerance);
            if (match < 0) return false;
            corners.RemoveAt(match);
        }
        // Reject a bow-tie control net with the same four corners.
        Point3d a = nurbs.Points.GetControlPoint(0, 0).Location;
        Point3d b = nurbs.Points.GetControlPoint(1, 0).Location;
        Point3d c = nurbs.Points.GetControlPoint(0, 1).Location;
        Point3d d = nurbs.Points.GetControlPoint(1, 1).Location;
        return (a + (b - a) + (c - a)).DistanceTo(d) <= tolerance;
    }
}
