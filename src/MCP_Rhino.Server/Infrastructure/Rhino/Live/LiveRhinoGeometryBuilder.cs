extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Arc = rhinocommon::Rhino.Geometry.Arc;
using ArcCurve = rhinocommon::Rhino.Geometry.ArcCurve;
using Circle = rhinocommon::Rhino.Geometry.Circle;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using Line = rhinocommon::Rhino.Geometry.Line;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using PlaneSurface = rhinocommon::Rhino.Geometry.PlaneSurface;
using Point = rhinocommon::Rhino.Geometry.Point;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryBuilder : ILiveGeometryBuilder
{
    public OperationResponse<GeometryBase> Build(GeometryCreationSpec spec)
    {
        try
        {
            GeometryBase geometry = spec.Primitive switch
            {
                GeometryPrimitiveKind.Point => new Point(new Point3d(spec.X, spec.Y, spec.Z)),
                GeometryPrimitiveKind.Line => new LineCurve(
                    new Line(
                        new Point3d(spec.StartX, spec.StartY, spec.StartZ),
                        new Point3d(spec.EndX, spec.EndY, spec.EndZ))),
                GeometryPrimitiveKind.Arc => BuildArc(spec),
                GeometryPrimitiveKind.Surface => BuildSurface(spec),
                _ => throw new InvalidOperationException($"Unsupported geometry primitive: {spec.Primitive}")
            };

            return OperationResponse<GeometryBase>.Ok(geometry);
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryBase>.Fail($"Failed to build live geometry: {ex.Message}");
        }
    }

    private static GeometryBase BuildArc(GeometryCreationSpec spec)
    {
        return spec.ArcMode switch
        {
            ArcConstructionMode.ThreePoint => new ArcCurve(new Arc(
                new Point3d(spec.StartX, spec.StartY, spec.StartZ),
                new Point3d(spec.MidX, spec.MidY, spec.MidZ),
                new Point3d(spec.EndX, spec.EndY, spec.EndZ))),
            ArcConstructionMode.CenterRadius => BuildCenterRadiusArc(spec),
            _ => throw new InvalidOperationException($"Unsupported arc construction mode: {spec.ArcMode}")
        };
    }

    private static GeometryBase BuildCenterRadiusArc(GeometryCreationSpec spec)
    {
        var plane = new Plane(
            new Point3d(spec.CenterX, spec.CenterY, spec.CenterZ),
            new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ));
        var circle = new Circle(plane, spec.Radius);
        var interval = new Interval(spec.StartAngleRadians, spec.EndAngleRadians);
        return new ArcCurve(new Arc(circle, interval));
    }

    private static GeometryBase BuildSurface(GeometryCreationSpec spec)
    {
        return spec.SurfaceMode switch
        {
            SurfaceConstructionMode.FourCorners => BuildFourCornerSurface(spec),
            SurfaceConstructionMode.Plane => new PlaneSurface(
                new Plane(
                    new Point3d(spec.OriginX, spec.OriginY, spec.OriginZ),
                    new Vector3d(spec.NormalX, spec.NormalY, spec.NormalZ)),
                new Interval(0d, spec.ULength),
                new Interval(0d, spec.VLength)),
            _ => throw new InvalidOperationException($"Unsupported surface construction mode: {spec.SurfaceMode}")
        };
    }

    private static GeometryBase BuildFourCornerSurface(GeometryCreationSpec spec)
    {
        NurbsSurface surface = NurbsSurface.Create(3, false, 2, 2, 2, 2);
        surface.Points.SetPoint(0, 0, new Point3d(spec.Corner0X, spec.Corner0Y, spec.Corner0Z));
        surface.Points.SetPoint(1, 0, new Point3d(spec.Corner1X, spec.Corner1Y, spec.Corner1Z));
        surface.Points.SetPoint(1, 1, new Point3d(spec.Corner2X, spec.Corner2Y, spec.Corner2Z));
        surface.Points.SetPoint(0, 1, new Point3d(spec.Corner3X, spec.Corner3Y, spec.Corner3Z));
        surface.KnotsU[0] = 0d;
        surface.KnotsU[1] = 1d;
        surface.KnotsV[0] = 0d;
        surface.KnotsV[1] = 1d;
        surface.SetDomain(0, new Interval(0d, 1d));
        surface.SetDomain(1, new Interval(0d, 1d));
        return surface;
    }
}
