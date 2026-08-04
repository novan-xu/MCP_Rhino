extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point = rhinocommon::Rhino.Geometry.Point;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;
using VolumeMassProperties = rhinocommon::Rhino.Geometry.VolumeMassProperties;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryMetricsCalculator : ILiveGeometryMetricsCalculator
{
    public OperationResponse<GeometryMetricsResult> GetObjectMetrics(Guid objectId, string geometryTypeName, GeometryBase geometry)
    {
        try
        {
            var result = new GeometryMetricsResult
            {
                ObjectId = objectId,
                GeometryTypeName = geometryTypeName,
                Success = true
            };

            switch (geometry)
            {
                case Curve curve:
                    result.Length = curve.GetLength();
                    result.IsClosed = curve.IsClosed;
                    break;
                case Surface surface:
                    result.IsClosed = surface.IsClosed(0) && surface.IsClosed(1);
                    using (AreaMassProperties? area = AreaMassProperties.Compute(surface))
                    {
                        result.Area = area?.Area;
                    }

                    using (Brep? surfaceBrep = surface.ToBrep())
                    {
                        result.Perimeter = surfaceBrep is null ? null : SumEdgeLengths(surfaceBrep);
                    }
                    break;
                case Brep brep:
                    result.IsClosed = brep.IsSolid;
                    using (AreaMassProperties? area = AreaMassProperties.Compute(brep))
                    {
                        result.Area = area?.Area;
                    }

                    result.Perimeter = SumEdgeLengths(brep);
                    if (brep.IsSolid)
                    {
                        using VolumeMassProperties? volume = VolumeMassProperties.Compute(brep);
                        result.Volume = volume?.Volume;
                    }
                    break;
                case Point:
                    result.IsClosed = false;
                    break;
                default:
                    return OperationResponse<GeometryMetricsResult>.Fail($"Unsupported geometry type for metrics: {geometryTypeName}");
            }

            return OperationResponse<GeometryMetricsResult>.Ok(result);
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryMetricsResult>.Fail($"Geometry metrics failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryDistanceResult> MeasureDistance(string entryId, ResolvedGeometryReference fromReference, ResolvedGeometryReference toReference, double tolerance)
    {
        try
        {
            if (LiveGeometryAnalysisHelpers.TryGetExplicitPoint(fromReference, out Point3d fromPoint))
            {
                return MeasurePointToReference(entryId, fromReference.GeometryTypeName, fromPoint, toReference, tolerance, forward: true);
            }

            if (LiveGeometryAnalysisHelpers.TryGetExplicitPoint(toReference, out Point3d toPoint))
            {
                return MeasurePointToReference(entryId, toReference.GeometryTypeName, toPoint, fromReference, tolerance, forward: false);
            }

            if (fromReference.Geometry is Curve fromCurve && toReference.Geometry is Curve toCurve)
            {
                if (!fromCurve.ClosestPoints(toCurve, out Point3d pointA, out Point3d pointB))
                {
                    return OperationResponse<GeometryDistanceResult>.Fail("Failed to compute closest points between the two curves.");
                }

                return OperationResponse<GeometryDistanceResult>.Ok(new GeometryDistanceResult
                {
                    EntryId = entryId,
                    FromGeometryTypeName = fromReference.GeometryTypeName,
                    ToGeometryTypeName = toReference.GeometryTypeName,
                    Success = true,
                    FromPoint = LiveGeometryAnalysisHelpers.ToPointData(pointA),
                    ToPoint = LiveGeometryAnalysisHelpers.ToPointData(pointB),
                    Distance = pointA.DistanceTo(pointB)
                });
            }

            return OperationResponse<GeometryDistanceResult>.Fail("Distance measurement requires at least one point-like reference or a curve-to-curve pair.");
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryDistanceResult>.Fail($"Distance measurement failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryAngleResult> MeasureThreePointAngle(string entryId, Point3d pointA, Point3d vertex, Point3d pointB)
    {
        Vector3d first = pointA - vertex;
        Vector3d second = pointB - vertex;
        return CreateAngleResult(entryId, GeometryAngleMeasurementMode.ThreePoints, first, second, vertex);
    }

    public OperationResponse<GeometryAngleResult> MeasureVectorAngle(string entryId, Vector3d firstVector, Vector3d secondVector)
    {
        return CreateAngleResult(entryId, GeometryAngleMeasurementMode.TwoVectors, firstVector, secondVector, null);
    }

    public OperationResponse<GeometryFrameResult> GetFrame(
        string entryId,
        Guid objectId,
        string geometryTypeName,
        GeometryBase geometry,
        GeometryFrameKind kind,
        double? parameter,
        double? u,
        double? v)
    {
        try
        {
            switch (kind)
            {
                case GeometryFrameKind.CurveStart:
                    if (geometry is not Curve startCurve)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("CurveStart requires a curve object.");
                    }

                    return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = objectId,
                        GeometryTypeName = geometryTypeName,
                        Kind = kind,
                        Success = true,
                        Origin = LiveGeometryAnalysisHelpers.ToPointData(startCurve.PointAtStart),
                        Tangent = LiveGeometryAnalysisHelpers.ToVectorData(startCurve.TangentAtStart)
                    });

                case GeometryFrameKind.CurveEnd:
                    if (geometry is not Curve endCurve)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("CurveEnd requires a curve object.");
                    }

                    return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = objectId,
                        GeometryTypeName = geometryTypeName,
                        Kind = kind,
                        Success = true,
                        Origin = LiveGeometryAnalysisHelpers.ToPointData(endCurve.PointAtEnd),
                        Tangent = LiveGeometryAnalysisHelpers.ToVectorData(endCurve.TangentAtEnd)
                    });

                case GeometryFrameKind.CurveTangent:
                    if (geometry is not Curve tangentCurve)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("CurveTangent requires a curve object.");
                    }

                    double curveParameter = parameter ?? tangentCurve.Domain.Mid;
                    Point3d curvePoint = tangentCurve.PointAt(curveParameter);
                    Vector3d curveTangent = tangentCurve.TangentAt(curveParameter);
                    return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = objectId,
                        GeometryTypeName = geometryTypeName,
                        Kind = kind,
                        Success = true,
                        Parameter = curveParameter,
                        Origin = LiveGeometryAnalysisHelpers.ToPointData(curvePoint),
                        Tangent = LiveGeometryAnalysisHelpers.ToVectorData(curveTangent)
                    });

                case GeometryFrameKind.SurfaceNormal:
                    if (geometry is not Surface normalSurface)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("SurfaceNormal requires a surface object.");
                    }

                    if (!u.HasValue || !v.HasValue)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("SurfaceNormal requires both U and V.");
                    }

                    return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = objectId,
                        GeometryTypeName = geometryTypeName,
                        Kind = kind,
                        Success = true,
                        U = u,
                        V = v,
                        Origin = LiveGeometryAnalysisHelpers.ToPointData(normalSurface.PointAt(u.Value, v.Value)),
                        Normal = LiveGeometryAnalysisHelpers.ToVectorData(normalSurface.NormalAt(u.Value, v.Value))
                    });

                case GeometryFrameKind.SurfaceFrame:
                    if (geometry is not Surface frameSurface)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("SurfaceFrame requires a surface object.");
                    }

                    if (!u.HasValue || !v.HasValue)
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("SurfaceFrame requires both U and V.");
                    }

                    if (!frameSurface.FrameAt(u.Value, v.Value, out Plane frame))
                    {
                        return OperationResponse<GeometryFrameResult>.Fail("Failed to evaluate surface frame at the requested UV.");
                    }

                    return OperationResponse<GeometryFrameResult>.Ok(new GeometryFrameResult
                    {
                        EntryId = entryId,
                        ObjectId = objectId,
                        GeometryTypeName = geometryTypeName,
                        Kind = kind,
                        Success = true,
                        U = u,
                        V = v,
                        Origin = LiveGeometryAnalysisHelpers.ToPointData(frame.Origin),
                        XAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.XAxis),
                        YAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.YAxis),
                        ZAxis = LiveGeometryAnalysisHelpers.ToVectorData(frame.ZAxis),
                        Normal = LiveGeometryAnalysisHelpers.ToVectorData(frame.ZAxis)
                    });

                default:
                    return OperationResponse<GeometryFrameResult>.Fail($"Unsupported frame kind: {kind}");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryFrameResult>.Fail($"Geometry frame evaluation failed: {ex.Message}");
        }
    }

    private static OperationResponse<GeometryDistanceResult> MeasurePointToReference(
        string entryId,
        string pointTypeName,
        Point3d point,
        ResolvedGeometryReference otherReference,
        double tolerance,
        bool forward)
    {
        if (!TryClosestPoint(otherReference.Geometry, point, tolerance, out Point3d otherPoint))
        {
            return OperationResponse<GeometryDistanceResult>.Fail("Failed to compute closest point on the target geometry.");
        }

        return OperationResponse<GeometryDistanceResult>.Ok(new GeometryDistanceResult
        {
            EntryId = entryId,
            FromGeometryTypeName = forward ? pointTypeName : otherReference.GeometryTypeName,
            ToGeometryTypeName = forward ? otherReference.GeometryTypeName : pointTypeName,
            Success = true,
            FromPoint = LiveGeometryAnalysisHelpers.ToPointData(forward ? point : otherPoint),
            ToPoint = LiveGeometryAnalysisHelpers.ToPointData(forward ? otherPoint : point),
            Distance = point.DistanceTo(otherPoint)
        });
    }

    private static bool TryClosestPoint(GeometryBase geometry, Point3d point, double tolerance, out Point3d closestPoint)
    {
        switch (geometry)
        {
            case Point pointGeometry:
                closestPoint = pointGeometry.Location;
                return true;
            case Curve curve:
                if (curve.ClosestPoint(point, out double curveParameter, tolerance))
                {
                    closestPoint = curve.PointAt(curveParameter);
                    return true;
                }
                break;
            case Surface surface:
                if (surface.ClosestPoint(point, out double u, out double v))
                {
                    closestPoint = surface.PointAt(u, v);
                    return true;
                }
                break;
            case Brep brep:
                foreach (var face in brep.Faces)
                {
                    if (face.ClosestPoint(point, out double faceU, out double faceV))
                    {
                        closestPoint = face.PointAt(faceU, faceV);
                        return true;
                    }
                }
                break;
        }

        closestPoint = Point3d.Unset;
        return false;
    }

    private static OperationResponse<GeometryAngleResult> CreateAngleResult(
        string entryId,
        GeometryAngleMeasurementMode mode,
        Vector3d firstVector,
        Vector3d secondVector,
        Point3d? vertex)
    {
        if (!firstVector.Unitize() || !secondVector.Unitize())
        {
            return OperationResponse<GeometryAngleResult>.Fail("Angle measurement requires non-zero vectors.");
        }

        double radians = Vector3d.VectorAngle(firstVector, secondVector);
        return OperationResponse<GeometryAngleResult>.Ok(new GeometryAngleResult
        {
            EntryId = entryId,
            Mode = mode,
            Success = true,
            AngleRadians = radians,
            AngleDegrees = radians * 180d / Math.PI,
            Vertex = vertex.HasValue ? LiveGeometryAnalysisHelpers.ToPointData(vertex.Value) : null,
            FirstDirection = LiveGeometryAnalysisHelpers.ToVectorData(firstVector),
            SecondDirection = LiveGeometryAnalysisHelpers.ToVectorData(secondVector)
        });
    }

    private static double SumEdgeLengths(Brep brep)
    {
        double total = 0d;
        foreach (var edge in brep.Edges)
        {
            total += edge.GetLength();
        }

        return total;
    }
}
