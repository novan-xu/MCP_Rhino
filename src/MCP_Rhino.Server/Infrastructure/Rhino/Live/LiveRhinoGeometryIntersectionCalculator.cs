extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using AreaMassProperties = rhinocommon::Rhino.Geometry.AreaMassProperties;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using CurveIntersections = rhinocommon::Rhino.Geometry.Intersect.CurveIntersections;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Intersection = rhinocommon::Rhino.Geometry.Intersect.Intersection;
using Point = rhinocommon::Rhino.Geometry.Point;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using VolumeMassProperties = rhinocommon::Rhino.Geometry.VolumeMassProperties;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoGeometryIntersectionCalculator : ILiveGeometryIntersectionCalculator
{
    public OperationResponse<GeometryMassResult> GetMassProperties(Guid objectId, string geometryTypeName, GeometryBase geometry, GeometryMassKind kind)
    {
        try
        {
            var result = new GeometryMassResult
            {
                ObjectId = objectId,
                GeometryTypeName = geometryTypeName,
                Kind = kind,
                Success = true
            };

            switch (geometry)
            {
                case Curve curve:
                    result.IsClosed = curve.IsClosed;
                    if (kind == GeometryMassKind.Length)
                    {
                        result.Length = curve.GetLength();
                        return OperationResponse<GeometryMassResult>.Ok(result);
                    }

                    if (kind is GeometryMassKind.Area or GeometryMassKind.Auto)
                    {
                        if (!curve.IsClosed || !curve.TryGetPlane(out _))
                        {
                            return OperationResponse<GeometryMassResult>.Fail("Open or non-planar curves are not supported by GetMassPropertiesInLive for Area/Auto. Use GetObjectMetricsInLive for length.");
                        }

                        using AreaMassProperties? area = AreaMassProperties.Compute(curve);
                        result.Area = area?.Area;
                        result.Centroid = area is null ? null : LiveGeometryAnalysisHelpers.ToPointData(area.Centroid);
                        return OperationResponse<GeometryMassResult>.Ok(result);
                    }

                    return OperationResponse<GeometryMassResult>.Fail("Curve mass properties support only Length or closed-planar Area/Auto.");

                case Surface surface:
                    if (kind == GeometryMassKind.Volume)
                    {
                        return OperationResponse<GeometryMassResult>.Fail("Surface geometry does not support volume mass properties.");
                    }

                    using (AreaMassProperties? area = AreaMassProperties.Compute(surface))
                    {
                        result.Area = area?.Area;
                        result.Centroid = area is null ? null : LiveGeometryAnalysisHelpers.ToPointData(area.Centroid);
                    }

                    result.IsClosed = surface.IsClosed(0) && surface.IsClosed(1);
                    return OperationResponse<GeometryMassResult>.Ok(result);

                case Brep brep:
                    result.IsClosed = brep.IsSolid;
                    if (kind == GeometryMassKind.Length)
                    {
                        return OperationResponse<GeometryMassResult>.Fail("Brep mass properties do not support Length. Use GetObjectMetricsInLive for perimeter.");
                    }

                    if (kind == GeometryMassKind.Volume || (kind == GeometryMassKind.Auto && brep.IsSolid))
                    {
                        if (!brep.IsSolid)
                        {
                            return OperationResponse<GeometryMassResult>.Fail("Open Brep does not support volume mass properties.");
                        }

                        using VolumeMassProperties? volume = VolumeMassProperties.Compute(brep);
                        result.Volume = volume?.Volume;
                        result.Centroid = volume is null ? null : LiveGeometryAnalysisHelpers.ToPointData(volume.Centroid);
                        return OperationResponse<GeometryMassResult>.Ok(result);
                    }

                    using (AreaMassProperties? brepArea = AreaMassProperties.Compute(brep))
                    {
                        result.Area = brepArea?.Area;
                        result.Centroid = brepArea is null ? null : LiveGeometryAnalysisHelpers.ToPointData(brepArea.Centroid);
                    }

                    return OperationResponse<GeometryMassResult>.Ok(result);

                default:
                    return OperationResponse<GeometryMassResult>.Fail($"Unsupported geometry type for mass properties: {geometryTypeName}");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryMassResult>.Fail($"Mass-properties calculation failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryIntersectionResult> Intersect(
        GeometryIntersectionEntryRequest entry,
        ResolvedGeometryReference firstReference,
        ResolvedGeometryReference secondReference,
        double tolerance)
    {
        try
        {
            GeometryIntersectionKind kind = entry.Kind == GeometryIntersectionKind.Auto
                ? InferIntersectionKind(firstReference.Geometry, secondReference.Geometry)
                : entry.Kind;

            var result = new GeometryIntersectionResult
            {
                EntryId = entry.EntryId,
                Kind = kind,
                Success = true,
                FirstGeometryTypeName = firstReference.GeometryTypeName,
                SecondGeometryTypeName = secondReference.GeometryTypeName
            };

            switch (kind)
            {
                case GeometryIntersectionKind.CurveCurve:
                    if (firstReference.Geometry is not Curve curveA || secondReference.Geometry is not Curve curveB)
                    {
                        return OperationResponse<GeometryIntersectionResult>.Fail("CurveCurve requires two curves.");
                    }

                    CurveIntersections? curveEvents = Intersection.CurveCurve(curveA, curveB, tolerance, tolerance);
                    if (curveEvents is not null)
                    {
                        foreach (var curveEvent in curveEvents)
                        {
                            if (curveEvent.IsPoint)
                            {
                                result.Points = result.Points.Concat(new[] { LiveGeometryAnalysisHelpers.ToPointData(curveEvent.PointA) }).ToList();
                            }

                            if (curveEvent.IsOverlap)
                            {
                                result.OverlapCurves = result.OverlapCurves.Concat(new[]
                                {
                                    CreateCurvePreview(curveA.Trim(curveEvent.OverlapA))
                                }).ToList();
                            }
                        }
                    }
                    break;

                case GeometryIntersectionKind.CurveSurface:
                case GeometryIntersectionKind.CurveBrep:
                    if (firstReference.Geometry is not Curve curve)
                    {
                        return OperationResponse<GeometryIntersectionResult>.Fail("CurveSurface/CurveBrep requires the first reference to be a curve.");
                    }

                    Brep? targetBrep = secondReference.Geometry switch
                    {
                        Surface surface => surface.ToBrep(),
                        Brep brep => brep,
                        _ => null
                    };
                    if (targetBrep is null)
                    {
                        return OperationResponse<GeometryIntersectionResult>.Fail("CurveSurface/CurveBrep requires the second reference to be a surface or brep.");
                    }

                    if (!Intersection.CurveBrep(curve, targetBrep, tolerance, out Curve[] overlapCurves, out Point3d[] intersectionPoints))
                    {
                        return OperationResponse<GeometryIntersectionResult>.Ok(result);
                    }

                    result.OverlapCurves = overlapCurves.Select(CreateCurvePreview).ToList();
                    result.Points = intersectionPoints.Select(LiveGeometryAnalysisHelpers.ToPointData).ToList();
                    break;

                case GeometryIntersectionKind.SurfaceSurface:
                case GeometryIntersectionKind.BrepBrep:
                    Brep? firstBrep = firstReference.Geometry switch
                    {
                        Surface surface => surface.ToBrep(),
                        Brep brep => brep,
                        _ => null
                    };
                    Brep? secondBrep = secondReference.Geometry switch
                    {
                        Surface surface => surface.ToBrep(),
                        Brep brep => brep,
                        _ => null
                    };
                    if (firstBrep is null || secondBrep is null)
                    {
                        return OperationResponse<GeometryIntersectionResult>.Fail("SurfaceSurface/BrepBrep requires both references to be surfaces or breps.");
                    }

                    if (!Intersection.BrepBrep(firstBrep, secondBrep, tolerance, out Curve[] curves, out Point3d[] points))
                    {
                        return OperationResponse<GeometryIntersectionResult>.Ok(result);
                    }

                    result.Curves = curves.Select(CreateCurvePreview).ToList();
                    result.Points = points.Select(LiveGeometryAnalysisHelpers.ToPointData).ToList();
                    break;

                default:
                    return OperationResponse<GeometryIntersectionResult>.Fail($"Unsupported intersection kind: {kind}");
            }

            return OperationResponse<GeometryIntersectionResult>.Ok(result);
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryIntersectionResult>.Fail($"Intersection analysis failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryClosestPointResult> GetClosestPoints(
        GeometryClosestPointEntryRequest entry,
        ResolvedGeometryReference sourceReference,
        ResolvedGeometryReference targetReference)
    {
        try
        {
            if (!LiveGeometryAnalysisHelpers.TryGetExplicitPoint(sourceReference, out Point3d sourcePoint))
            {
                return OperationResponse<GeometryClosestPointResult>.Fail("Closest-point analysis requires the Source reference to resolve to a point or explicit curve/surface parameter.");
            }

            if (!TryClosestPoint(targetReference, sourcePoint, out Point3d targetPoint, out double? parameter, out double? u, out double? v))
            {
                return OperationResponse<GeometryClosestPointResult>.Fail("Failed to compute closest point on the target geometry.");
            }

            return OperationResponse<GeometryClosestPointResult>.Ok(new GeometryClosestPointResult
            {
                EntryId = entry.EntryId,
                TargetKind = entry.TargetKind,
                Success = true,
                SourceGeometryTypeName = sourceReference.GeometryTypeName,
                TargetGeometryTypeName = targetReference.GeometryTypeName,
                SourcePoint = LiveGeometryAnalysisHelpers.ToPointData(sourcePoint),
                TargetPoint = LiveGeometryAnalysisHelpers.ToPointData(targetPoint),
                Distance = sourcePoint.DistanceTo(targetPoint),
                SourceParameter = sourceReference.Parameter,
                SourceU = sourceReference.U,
                SourceV = sourceReference.V,
                TargetParameter = parameter,
                TargetU = u,
                TargetV = v
            });
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryClosestPointResult>.Fail($"Closest-point analysis failed: {ex.Message}");
        }
    }

    public OperationResponse<GeometryContourResult> GetContourCurves(
        GeometryContourEntryRequest entry,
        Guid objectId,
        string geometryTypeName,
        GeometryBase geometry)
    {
        try
        {
            Point3d start = new(entry.StartX, entry.StartY, entry.StartZ);
            Point3d end = new(entry.EndX, entry.EndY, entry.EndZ);
            if (entry.Interval <= 0d)
            {
                return OperationResponse<GeometryContourResult>.Fail("Contour interval must be greater than zero.");
            }

            Brep? contourBrep = geometry switch
            {
                Surface surface => surface.ToBrep(),
                Brep brep => brep,
                _ => null
            };

            if (contourBrep is null)
            {
                return OperationResponse<GeometryContourResult>.Fail($"Unsupported geometry type for contour curves: {geometryTypeName}");
            }

            Curve[] curves = Brep.CreateContourCurves(contourBrep, start, end, entry.Interval);

            return OperationResponse<GeometryContourResult>.Ok(new GeometryContourResult
            {
                EntryId = entry.EntryId,
                ObjectId = objectId,
                GeometryTypeName = geometryTypeName,
                Success = true,
                Curves = curves.Select(CreateCurvePreview).ToList()
            });
        }
        catch (Exception ex)
        {
            return OperationResponse<GeometryContourResult>.Fail($"Contour-curve generation failed: {ex.Message}");
        }
    }

    private static GeometryIntersectionKind InferIntersectionKind(GeometryBase firstGeometry, GeometryBase secondGeometry)
    {
        return (firstGeometry, secondGeometry) switch
        {
            (Curve, Curve) => GeometryIntersectionKind.CurveCurve,
            (Curve, Surface) => GeometryIntersectionKind.CurveSurface,
            (Curve, Brep) => GeometryIntersectionKind.CurveBrep,
            (Surface, Curve) => GeometryIntersectionKind.CurveSurface,
            (Surface, Surface) => GeometryIntersectionKind.SurfaceSurface,
            (Surface, Brep) => GeometryIntersectionKind.BrepBrep,
            (Brep, Curve) => GeometryIntersectionKind.CurveBrep,
            (Brep, Surface) => GeometryIntersectionKind.BrepBrep,
            (Brep, Brep) => GeometryIntersectionKind.BrepBrep,
            _ => throw new InvalidOperationException("Unable to infer an intersection kind for the provided geometry pair.")
        };
    }

    private static bool TryClosestPoint(
        ResolvedGeometryReference targetReference,
        Point3d sourcePoint,
        out Point3d targetPoint,
        out double? parameter,
        out double? u,
        out double? v)
    {
        parameter = null;
        u = null;
        v = null;

        switch (targetReference.Geometry)
        {
            case Point point:
                targetPoint = point.Location;
                return true;
            case Curve curve:
                if (curve.ClosestPoint(sourcePoint, out double curveParameter))
                {
                    parameter = curveParameter;
                    targetPoint = curve.PointAt(curveParameter);
                    return true;
                }
                break;
            case Surface surface:
                if (surface.ClosestPoint(sourcePoint, out double surfaceU, out double surfaceV))
                {
                    u = surfaceU;
                    v = surfaceV;
                    targetPoint = surface.PointAt(surfaceU, surfaceV);
                    return true;
                }
                break;
            case Brep brep:
                foreach (var face in brep.Faces)
                {
                    if (face.ClosestPoint(sourcePoint, out double faceU, out double faceV))
                    {
                        u = faceU;
                        v = faceV;
                        targetPoint = face.PointAt(faceU, faceV);
                        return true;
                    }
                }
                break;
        }

        targetPoint = Point3d.Unset;
        return false;
    }

    private static GeometryCurvePreview CreateCurvePreview(Curve? curve)
    {
        if (curve is null)
        {
            return new GeometryCurvePreview();
        }

        List<GeometryPointData> samplePoints = new();
        double[]? parameters = curve.DivideByCount(8, true);
        if (parameters is not null && parameters.Length > 0)
        {
            samplePoints.AddRange(parameters.Select(parameter => LiveGeometryAnalysisHelpers.ToPointData(curve.PointAt(parameter))));
        }
        else
        {
            samplePoints.Add(LiveGeometryAnalysisHelpers.ToPointData(curve.PointAtStart));
            samplePoints.Add(LiveGeometryAnalysisHelpers.ToPointData(curve.PointAtEnd));
        }

        return new GeometryCurvePreview
        {
            CurveTypeName = curve.GetType().Name,
            IsClosed = curve.IsClosed,
            Length = curve.GetLength(),
            SamplePoints = samplePoints
        };
    }
}
