using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services.Rebuild;

public sealed class SurfaceBoundaryPointOrderer : ISurfaceBoundaryPointOrderer
{
    public OperationResponse<SurfacePointOrderPlan> Order(
        SurfaceRebuildDescriptor descriptor,
        SurfaceReferenceCurveSpec referenceCurve,
        SurfaceLocalCoordinateSystem localFrame,
        SurfaceRebuildSpec spec)
    {
        if (descriptor.OuterBoundaryLoop.Vertices3d.Count < 3)
        {
            return OperationResponse<SurfacePointOrderPlan>.Fail("INSUFFICIENT_ORDERABLE_POINTS");
        }

        if (localFrame.IsDegenerate)
        {
            return OperationResponse<SurfacePointOrderPlan>.Fail("DEGENERATE_LOCAL_FRAME");
        }

        var projected = new List<OrderedSurfacePoint>();
        for (int i = 0; i < descriptor.OuterBoundaryLoop.Vertices3d.Count; i++)
        {
            GeometryPointData point = descriptor.OuterBoundaryLoop.Vertices3d[i];
            GeometryPoint2dData uv = Project(point, localFrame);
            double angle = Math.Atan2(uv.Y, uv.X);
            projected.Add(new OrderedSurfacePoint
            {
                OriginalIndex = i,
                SortKey = angle,
                Position3d = point,
                Position2dInLcs = uv
            });
        }

        List<OrderedSurfacePoint> ordered = spec.Direction == SurfacePointOrderDirection.Clockwise
            ? projected.OrderByDescending(point => point.SortKey).ThenBy(point => Distance2d(point.Position2dInLcs)).ToList()
            : projected.OrderBy(point => point.SortKey).ThenBy(point => Distance2d(point.Position2dInLcs)).ToList();

        int anchor = ResolveAnchor(ordered, referenceCurve, spec.StartAnchorMode);
        ordered = ordered.Skip(anchor).Concat(ordered.Take(anchor)).ToList();

        if (HasSelfIntersection(ordered))
        {
            return OperationResponse<SurfacePointOrderPlan>.Fail("SELF_INTERSECTING_BOUNDARY");
        }

        var warnings = new List<string>();
        if (ordered.Select(point => point.OriginalIndex).SequenceEqual(Enumerable.Range(0, ordered.Count)))
        {
            warnings.Add("POINT_ORDER_UNCHANGED");
        }

        SurfaceRebuildRouteKind route = descriptor.Topology == SurfaceTopologyKind.Quad && ordered.Count == 4
            ? SurfaceRebuildRouteKind.FourPoint
            : SurfaceRebuildRouteKind.BoundarySurface;

        return OperationResponse<SurfacePointOrderPlan>.Ok(new SurfacePointOrderPlan
        {
            ObjectId = descriptor.ObjectId,
            Points = ordered,
            Direction = spec.Direction,
            StartAnchorIndex = ordered[0].OriginalIndex,
            ReferenceCurve = referenceCurve,
            LocalFrame = localFrame,
            Route = route,
            Topology = descriptor.Topology,
            PreserveBrepType = descriptor.PreserveBrepType,
            Warnings = warnings
        });
    }

    private static GeometryPoint2dData Project(GeometryPointData point, SurfaceLocalCoordinateSystem localFrame)
    {
        double dx = point.X - localFrame.Origin.X;
        double dy = point.Y - localFrame.Origin.Y;
        double dz = point.Z - localFrame.Origin.Z;
        return new GeometryPoint2dData
        {
            X = Dot(dx, dy, dz, localFrame.XAxis),
            Y = Dot(dx, dy, dz, localFrame.YAxis)
        };
    }

    private static int ResolveAnchor(
        IReadOnlyList<OrderedSurfacePoint> ordered,
        SurfaceReferenceCurveSpec referenceCurve,
        SurfacePointOrderStartAnchorMode mode)
    {
        if (mode == SurfacePointOrderStartAnchorMode.MinAngle)
        {
            return 0;
        }

        if (mode == SurfacePointOrderStartAnchorMode.ClosestToOrigin)
        {
            double bestDistance = double.MaxValue;
            int bestIndex = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                double distance = Distance2d(ordered[i].Position2dInLcs);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        double best = double.MaxValue;
        int anchor = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            double distance = Distance3d(ordered[i].Position3d, referenceCurve.StartPoint);
            if (distance < best)
            {
                best = distance;
                anchor = i;
            }
        }

        return anchor;
    }

    private static bool HasSelfIntersection(IReadOnlyList<OrderedSurfacePoint> points)
    {
        if (points.Count < 4)
        {
            return false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            GeometryPoint2dData a1 = points[i].Position2dInLcs;
            GeometryPoint2dData a2 = points[(i + 1) % points.Count].Position2dInLcs;
            for (int j = i + 1; j < points.Count; j++)
            {
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == points.Count - 1))
                {
                    continue;
                }

                GeometryPoint2dData b1 = points[j].Position2dInLcs;
                GeometryPoint2dData b2 = points[(j + 1) % points.Count].Position2dInLcs;
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(
        GeometryPoint2dData a1,
        GeometryPoint2dData a2,
        GeometryPoint2dData b1,
        GeometryPoint2dData b2)
    {
        double d1 = Cross(a1, a2, b1);
        double d2 = Cross(a1, a2, b2);
        double d3 = Cross(b1, b2, a1);
        double d4 = Cross(b1, b2, a2);
        const double tolerance = 1e-9;
        return ((d1 > tolerance && d2 < -tolerance) || (d1 < -tolerance && d2 > tolerance))
            && ((d3 > tolerance && d4 < -tolerance) || (d3 < -tolerance && d4 > tolerance));
    }

    private static double Cross(GeometryPoint2dData a, GeometryPoint2dData b, GeometryPoint2dData c)
    {
        return ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
    }

    private static double Dot(double x, double y, double z, GeometryVectorData axis)
    {
        return (x * axis.X) + (y * axis.Y) + (z * axis.Z);
    }

    private static double Distance2d(GeometryPoint2dData point)
    {
        return Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
    }

    private static double Distance3d(GeometryPointData a, GeometryPointData b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
