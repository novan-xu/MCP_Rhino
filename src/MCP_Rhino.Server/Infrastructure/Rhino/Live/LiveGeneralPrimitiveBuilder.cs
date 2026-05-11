extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using ArcCurve = rhinocommon::Rhino.Geometry.ArcCurve;
using BlendType = rhinocommon::Rhino.Geometry.BlendType;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Box = rhinocommon::Rhino.Geometry.Box;
using Circle = rhinocommon::Rhino.Geometry.Circle;
using Cone = rhinocommon::Rhino.Geometry.Cone;
using Cylinder = rhinocommon::Rhino.Geometry.Cylinder;
using Ellipse = rhinocommon::Rhino.Geometry.Ellipse;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using Mesh = rhinocommon::Rhino.Geometry.Mesh;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PipeCapMode = rhinocommon::Rhino.Geometry.PipeCapMode;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using RailType = rhinocommon::Rhino.Geometry.RailType;
using Sphere = rhinocommon::Rhino.Geometry.Sphere;
using Torus = rhinocommon::Rhino.Geometry.Torus;
using Transform = rhinocommon::Rhino.Geometry.Transform;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeneralPrimitiveBuilder : ILiveGeneralPrimitiveBuilder
{
    public OperationResponse<GeometryBase> Build(GeneralPrimitiveCreationSpec spec)
    {
        try
        {
            GeometryBase geometry = spec.Kind switch
            {
                GeneralPrimitiveKind.Circle => BuildCircle(spec),
                GeneralPrimitiveKind.Ellipse => BuildEllipse(spec),
                GeneralPrimitiveKind.Polyline => BuildPolyline(spec),
                GeneralPrimitiveKind.NurbsCurve => BuildNurbsCurve(spec),
                GeneralPrimitiveKind.Sphere => BuildSphere(spec),
                GeneralPrimitiveKind.Cone => BuildCone(spec),
                GeneralPrimitiveKind.Cylinder => BuildCylinder(spec),
                GeneralPrimitiveKind.Ellipsoid => BuildEllipsoid(spec),
                GeneralPrimitiveKind.Capsule => BuildCapsule(spec),
                GeneralPrimitiveKind.Torus => BuildTorus(spec),
                GeneralPrimitiveKind.RoundedBox => BuildRoundedBox(spec),
                GeneralPrimitiveKind.RaisedStrip => BuildRaisedStrip(spec),
                GeneralPrimitiveKind.TaperedBox => BuildTaperedBox(spec),
                _ => throw new InvalidOperationException($"Unsupported general primitive kind: {spec.Kind}")
            };

            if (!geometry.IsValid)
            {
                return OperationResponse<GeometryBase>.Fail($"Built {spec.Kind} geometry is invalid.");
            }

            return OperationResponse<GeometryBase>.Ok(geometry);
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryBase>.Fail($"Failed to build {spec.Kind}: {ex.Message}");
        }
    }

    private static GeometryBase BuildCircle(GeneralPrimitiveCreationSpec spec)
    {
        var plane = new Plane(
            new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ));
        return new ArcCurve(new Circle(plane, spec.Radius));
    }

    private static GeometryBase BuildEllipse(GeneralPrimitiveCreationSpec spec)
    {
        Plane plane = BuildOrientedPlane(
            new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ),
            new Vector3d(spec.XAxisX, spec.XAxisY, spec.XAxisZ));
        return new Ellipse(plane, spec.RadiusX, spec.RadiusY).ToNurbsCurve();
    }

    private static GeometryBase BuildPolyline(GeneralPrimitiveCreationSpec spec)
    {
        List<Point3d> points = spec.Points.Select(ToPoint3d).ToList();
        if (spec.Closed && points.Count > 0 && !points[0].EpsilonEquals(points[^1], 1e-12))
        {
            points.Add(points[0]);
        }

        return new PolylineCurve(new Polyline(points));
    }

    private static GeometryBase BuildNurbsCurve(GeneralPrimitiveCreationSpec spec)
    {
        NurbsCurve? curve = NurbsCurve.Create(
            spec.Closed,
            spec.Degree,
            spec.Points.Select(ToPoint3d));
        return curve ?? throw new InvalidOperationException("Rhino failed to create a NurbsCurve from the supplied control points.");
    }

    private static GeometryBase BuildSphere(GeneralPrimitiveCreationSpec spec)
    {
        return new Sphere(new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ), spec.Radius).ToBrep();
    }

    private static GeometryBase BuildCone(GeneralPrimitiveCreationSpec spec)
    {
        var plane = new Plane(
            new Point3d(spec.BaseX, spec.BaseY, spec.BaseZ),
            new Vector3d(spec.AxisX, spec.AxisY, spec.AxisZ));
        Brep? brep = new Cone(plane, spec.Height, spec.Radius).ToBrep(spec.CapBottom);
        return brep ?? throw new InvalidOperationException("Rhino failed to create cone Brep geometry.");
    }

    private static GeometryBase BuildCylinder(GeneralPrimitiveCreationSpec spec)
    {
        var plane = new Plane(
            new Point3d(spec.BaseX, spec.BaseY, spec.BaseZ),
            new Vector3d(spec.AxisX, spec.AxisY, spec.AxisZ));
        var cylinder = new Cylinder(new Circle(plane, spec.Radius), spec.Height);
        Brep? brep = cylinder.ToBrep(spec.CapBottom, spec.CapTop);
        return brep ?? throw new InvalidOperationException("Rhino failed to create cylinder Brep geometry.");
    }

    private static GeometryBase BuildEllipsoid(GeneralPrimitiveCreationSpec spec)
    {
        Point3d center = new(spec.CenterX, spec.CenterY, spec.CenterZ);
        Plane plane = BuildOrientedPlane(
            center,
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ),
            new Vector3d(spec.XAxisX, spec.XAxisY, spec.XAxisZ));

        Brep? brep = new Sphere(center, 1d).ToBrep();
        if (brep is null)
        {
            throw new InvalidOperationException("Rhino failed to create source sphere Brep geometry.");
        }

        bool transformed = brep.Transform(Transform.Scale(plane, spec.RadiusX, spec.RadiusY, spec.RadiusZ));
        if (!transformed)
        {
            throw new InvalidOperationException("Rhino failed to scale source sphere into ellipsoid geometry.");
        }

        return brep;
    }

    private static GeometryBase BuildCapsule(GeneralPrimitiveCreationSpec spec)
    {
        var rail = new LineCurve(
            new Point3d(spec.BaseX, spec.BaseY, spec.BaseZ),
            new Point3d(spec.EndX, spec.EndY, spec.EndZ));
        Brep[]? breps = Brep.CreatePipe(
            rail,
            spec.Radius,
            false,
            PipeCapMode.Round,
            true,
            spec.Tolerance,
            Math.PI / 180d);
        return JoinSingleBrep(breps, spec.Tolerance, "capsule");
    }

    private static GeometryBase BuildTorus(GeneralPrimitiveCreationSpec spec)
    {
        Plane plane = BuildOrientedPlane(
            new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ),
            new Vector3d(spec.XAxisX, spec.XAxisY, spec.XAxisZ));
        Brep? brep = new Torus(plane, spec.MajorRadius, spec.MinorRadius).ToBrep();
        return brep ?? throw new InvalidOperationException("Rhino failed to create torus Brep geometry.");
    }

    private static GeometryBase BuildRoundedBox(GeneralPrimitiveCreationSpec spec)
    {
        Plane plane = BuildOrientedPlane(
            new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ),
            new Vector3d(spec.XAxisX, spec.XAxisY, spec.XAxisZ));
        var box = new Box(
            plane,
            new Interval(-spec.Width * 0.5d, spec.Width * 0.5d),
            new Interval(-spec.Depth * 0.5d, spec.Depth * 0.5d),
            new Interval(-spec.Height * 0.5d, spec.Height * 0.5d));

        Brep? baseBrep = Brep.CreateFromBox(box);
        if (baseBrep is null)
        {
            throw new InvalidOperationException("Rhino failed to create source box Brep geometry.");
        }

        int[] edgeIndices = Enumerable.Range(0, baseBrep.Edges.Count).ToArray();
        double[] radii = Enumerable.Repeat(spec.Radius, edgeIndices.Length).ToArray();
        Brep[]? filleted = Brep.CreateFilletEdges(
            baseBrep,
            edgeIndices,
            radii,
            radii,
            BlendType.Fillet,
            RailType.RollingBall,
            spec.Tolerance);

        return JoinSingleBrep(filleted, spec.Tolerance, "rounded box");
    }

    private static GeometryBase BuildRaisedStrip(GeneralPrimitiveCreationSpec spec)
    {
        Point3d start = new(spec.BaseX, spec.BaseY, spec.BaseZ);
        Point3d end = new(spec.EndX, spec.EndY, spec.EndZ);
        var direction = end - start;
        double length = direction.Length;
        var up = new Vector3d(spec.UpX, spec.UpY, spec.UpZ);
        up.Unitize();
        Point3d center = start + (direction * 0.5d) + (up * (spec.Height * 0.5d));
        Plane plane = BuildFrameFromXAxisAndUp(center, direction, up);
        var box = new Box(
            plane,
            new Interval(-length * 0.5d, length * 0.5d),
            new Interval(-spec.Width * 0.5d, spec.Width * 0.5d),
            new Interval(-spec.Height * 0.5d, spec.Height * 0.5d));
        Brep? brep = Brep.CreateFromBox(box);
        return brep ?? throw new InvalidOperationException("Rhino failed to create raised strip Brep geometry.");
    }

    private static GeometryBase BuildTaperedBox(GeneralPrimitiveCreationSpec spec)
    {
        Point3d start = new(spec.BaseX, spec.BaseY, spec.BaseZ);
        Point3d end = new(spec.EndX, spec.EndY, spec.EndZ);
        var axis = end - start;
        if (!axis.Unitize())
        {
            throw new InvalidOperationException("TaperedBox start and end points must be distinct.");
        }

        var up = new Vector3d(spec.UpX, spec.UpY, spec.UpZ);
        if (!up.Unitize())
        {
            throw new InvalidOperationException("TaperedBox up vector cannot be zero.");
        }

        Vector3d widthAxis = Vector3d.CrossProduct(axis, up);
        if (!widthAxis.Unitize())
        {
            throw new InvalidOperationException("TaperedBox up vector cannot be parallel to its start-end direction.");
        }

        Vector3d depthAxis = Vector3d.CrossProduct(widthAxis, axis);
        depthAxis.Unitize();

        Point3d[] startCorners = BuildSectionCorners(start, widthAxis, depthAxis, spec.StartWidth, spec.StartDepth);
        Point3d[] endCorners = BuildSectionCorners(end, widthAxis, depthAxis, spec.EndWidth, spec.EndDepth);

        var mesh = new Mesh();
        foreach (Point3d point in startCorners.Concat(endCorners))
        {
            mesh.Vertices.Add(point);
        }

        mesh.Faces.AddFace(0, 3, 2, 1);
        mesh.Faces.AddFace(4, 5, 6, 7);
        mesh.Faces.AddFace(0, 1, 5, 4);
        mesh.Faces.AddFace(1, 2, 6, 5);
        mesh.Faces.AddFace(2, 3, 7, 6);
        mesh.Faces.AddFace(3, 0, 4, 7);
        mesh.Normals.ComputeNormals();
        mesh.Compact();
        return mesh;
    }

    private static Point3d[] BuildSectionCorners(
        Point3d center,
        Vector3d widthAxis,
        Vector3d depthAxis,
        double width,
        double depth)
    {
        Vector3d halfWidth = widthAxis * (width * 0.5d);
        Vector3d halfDepth = depthAxis * (depth * 0.5d);
        return new[]
        {
            center - halfWidth - halfDepth,
            center + halfWidth - halfDepth,
            center + halfWidth + halfDepth,
            center - halfWidth + halfDepth
        };
    }

    private static Plane BuildOrientedPlane(Point3d origin, Vector3d normal, Vector3d xAxis)
    {
        if (!normal.Unitize())
        {
            throw new InvalidOperationException("Plane normal vector cannot be zero.");
        }

        if (!xAxis.Unitize())
        {
            throw new InvalidOperationException("Plane X axis vector cannot be zero.");
        }

        Vector3d yAxis = Vector3d.CrossProduct(normal, xAxis);
        if (!yAxis.Unitize())
        {
            throw new InvalidOperationException("Plane X axis cannot be parallel to the normal vector.");
        }

        xAxis = Vector3d.CrossProduct(yAxis, normal);
        xAxis.Unitize();
        return new Plane(origin, xAxis, yAxis);
    }

    private static Plane BuildFrameFromXAxisAndUp(Point3d origin, Vector3d xAxis, Vector3d up)
    {
        if (!xAxis.Unitize())
        {
            throw new InvalidOperationException("Frame X axis vector cannot be zero.");
        }

        if (!up.Unitize())
        {
            throw new InvalidOperationException("Frame up vector cannot be zero.");
        }

        Vector3d yAxis = Vector3d.CrossProduct(up, xAxis);
        if (!yAxis.Unitize())
        {
            throw new InvalidOperationException("Frame up vector cannot be parallel to the X axis.");
        }

        return new Plane(origin, xAxis, yAxis);
    }

    private static Brep JoinSingleBrep(Brep[]? breps, double tolerance, string label)
    {
        if (breps is null || breps.Length == 0)
        {
            throw new InvalidOperationException($"Rhino failed to create {label} Brep geometry.");
        }

        if (breps.Length == 1)
        {
            return breps[0];
        }

        Brep[]? joined = Brep.JoinBreps(breps, tolerance);
        if (joined is { Length: 1 })
        {
            return joined[0];
        }

        throw new InvalidOperationException($"Rhino created {breps.Length} disconnected Breps for {label} geometry.");
    }

    private static Point3d ToPoint3d(GeneralPrimitivePointSpec point)
    {
        return new Point3d(point.X, point.Y, point.Z);
    }
}
