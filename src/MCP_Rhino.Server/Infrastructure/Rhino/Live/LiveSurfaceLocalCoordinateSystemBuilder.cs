using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveSurfaceLocalCoordinateSystemBuilder : ISurfaceLocalCoordinateSystemBuilder
{
    private const double Tolerance = 1e-12;

    public OperationResponse<SurfaceLocalCoordinateSystem> Build(SurfaceBoundaryLoop boundaryLoop)
    {
        GeometryVectorData normal = Normalize(boundaryLoop.FrontNormal);
        if (IsZero(normal))
        {
            normal = Normalize(ComputeNormal(boundaryLoop.Vertices3d));
        }

        if (IsZero(normal))
        {
            return OperationResponse<SurfaceLocalCoordinateSystem>.Fail("DEGENERATE_SURFACE_NORMAL");
        }

        GeometryVectorData gravity = new() { X = 0d, Y = 0d, Z = -1d };
        double gravityAlongNormal = Dot(gravity, normal);
        GeometryVectorData down = Normalize(new GeometryVectorData
        {
            X = gravity.X - (gravityAlongNormal * normal.X),
            Y = gravity.Y - (gravityAlongNormal * normal.Y),
            Z = gravity.Z - (gravityAlongNormal * normal.Z)
        });
        if (IsZero(down))
        {
            return OperationResponse<SurfaceLocalCoordinateSystem>.Fail("SURFACE_GRAVITY_PROJECTION_DEGENERATE");
        }

        GeometryVectorData yAxis = new()
        {
            X = -down.X,
            Y = -down.Y,
            Z = -down.Z
        };
        GeometryVectorData xAxis = Normalize(Cross(normal, down));
        bool degenerate = IsZero(xAxis) || IsZero(normal) || IsZero(yAxis);

        return OperationResponse<SurfaceLocalCoordinateSystem>.Ok(new SurfaceLocalCoordinateSystem
        {
            Origin = ComputeCentroid(boundaryLoop.Vertices3d),
            XAxis = xAxis,
            YAxis = yAxis,
            DownAxis = down,
            Normal = normal,
            IsDegenerate = degenerate
        });
    }

    private static GeometryVectorData ComputeNormal(IReadOnlyList<GeometryPointData> points)
    {
        double x = 0d;
        double y = 0d;
        double z = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            GeometryPointData current = points[i];
            GeometryPointData next = points[(i + 1) % points.Count];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new GeometryVectorData { X = x, Y = y, Z = z };
    }

    private static GeometryVectorData Subtract(GeometryPointData end, GeometryPointData start)
    {
        return new GeometryVectorData
        {
            X = end.X - start.X,
            Y = end.Y - start.Y,
            Z = end.Z - start.Z
        };
    }

    private static GeometryPointData ComputeCentroid(IReadOnlyList<GeometryPointData> points)
    {
        if (points.Count == 0)
        {
            return new GeometryPointData();
        }

        double x = 0d;
        double y = 0d;
        double z = 0d;
        foreach (GeometryPointData point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        return new GeometryPointData
        {
            X = x / points.Count,
            Y = y / points.Count,
            Z = z / points.Count
        };
    }

    private static GeometryVectorData Cross(GeometryVectorData a, GeometryVectorData b)
    {
        return new GeometryVectorData
        {
            X = (a.Y * b.Z) - (a.Z * b.Y),
            Y = (a.Z * b.X) - (a.X * b.Z),
            Z = (a.X * b.Y) - (a.Y * b.X)
        };
    }

    private static GeometryVectorData Normalize(GeometryVectorData vector)
    {
        double length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (length <= Tolerance || double.IsNaN(length) || double.IsInfinity(length))
        {
            return new GeometryVectorData();
        }

        return new GeometryVectorData
        {
            X = vector.X / length,
            Y = vector.Y / length,
            Z = vector.Z / length
        };
    }

    private static bool IsZero(GeometryVectorData vector)
    {
        return Math.Abs(vector.X) <= Tolerance && Math.Abs(vector.Y) <= Tolerance && Math.Abs(vector.Z) <= Tolerance;
    }

    private static double Dot(GeometryVectorData a, GeometryVectorData b)
    {
        return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    }
}
