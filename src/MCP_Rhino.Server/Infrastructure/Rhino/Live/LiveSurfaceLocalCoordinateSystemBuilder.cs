using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveSurfaceLocalCoordinateSystemBuilder : ISurfaceLocalCoordinateSystemBuilder
{
    public OperationResponse<SurfaceLocalCoordinateSystem> Build(
        SurfaceBoundaryLoop boundaryLoop,
        SurfaceReferenceCurveSpec referenceCurve)
    {
        GeometryVectorData xAxis = Normalize(Subtract(referenceCurve.EndPoint, referenceCurve.StartPoint));
        GeometryVectorData normal = Normalize(ComputeNormal(boundaryLoop.Vertices3d));
        GeometryVectorData yAxis = Normalize(Cross(normal, xAxis));
        bool degenerate = IsZero(xAxis) || IsZero(normal) || IsZero(yAxis);

        return OperationResponse<SurfaceLocalCoordinateSystem>.Ok(new SurfaceLocalCoordinateSystem
        {
            Origin = referenceCurve.Midpoint,
            XAxis = xAxis,
            YAxis = yAxis,
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
        if (length <= 1e-12 || double.IsNaN(length) || double.IsInfinity(length))
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
        return Math.Abs(vector.X) <= 1e-12 && Math.Abs(vector.Y) <= 1e-12 && Math.Abs(vector.Z) <= 1e-12;
    }
}
