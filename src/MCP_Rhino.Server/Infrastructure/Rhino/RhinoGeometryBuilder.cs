using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Rhino.Geometry;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoGeometryBuilder : IGeometryBuilder
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
                _ => throw new InvalidOperationException($"不支持的几何类型: {spec.Primitive}")
            };

            return OperationResponse<GeometryBase>.Ok(geometry);
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryBase>.Fail($"构建几何失败: {ex.Message}");
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
            _ => throw new InvalidOperationException($"不支持的圆弧构造模式: {spec.ArcMode}")
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
            _ => throw new InvalidOperationException($"不支持的曲面构造模式: {spec.SurfaceMode}")
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
