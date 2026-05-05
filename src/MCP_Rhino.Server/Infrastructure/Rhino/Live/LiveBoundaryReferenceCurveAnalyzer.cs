extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepFace = rhinocommon::Rhino.Geometry.BrepFace;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBoundaryReferenceCurveAnalyzer : IBoundaryReferenceCurveAnalyzer
{
    private const double LocalTolerance = 1e-9;
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ISurfaceLocalCoordinateSystemBuilder _localCoordinateSystemBuilder;

    public LiveBoundaryReferenceCurveAnalyzer(
        ILiveRhinoDocumentAccessor documentAccessor,
        ISurfaceLocalCoordinateSystemBuilder localCoordinateSystemBuilder)
    {
        _documentAccessor = documentAccessor;
        _localCoordinateSystemBuilder = localCoordinateSystemBuilder;
    }

    public OperationResponse<SurfaceRebuildDescriptor> Analyze(
        string filePath,
        Guid objectId,
        SurfaceRebuildSpec spec)
    {
        if (objectId == Guid.Empty)
        {
            return OperationResponse<SurfaceRebuildDescriptor>.Fail("ObjectId cannot be empty.");
        }

        return _documentAccessor.Execute(filePath, document =>
        {
            var rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is null)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail($"OBJECT_NOT_FOUND: {objectId}");
            }

            OperationResponse<(SurfaceBoundaryLoop Loop, string GeometryKind, bool PreserveBrepType)> loop = ExtractBoundaryLoop(
                rhinoObject.Geometry,
                document.ModelAbsoluteTolerance);
            if (!loop.Success)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail(loop.Message);
            }

            OperationResponse<SurfaceLocalCoordinateSystem> localFrame = _localCoordinateSystemBuilder.Build(loop.Data.Loop);
            if (!localFrame.Success || localFrame.Data is null)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail(localFrame.Message);
            }

            OperationResponse<List<SurfaceReferenceCurveSpec>> candidates = CreateCandidates(loop.Data.Loop, localFrame.Data);
            if (!candidates.Success || candidates.Data is null)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail(candidates.Message);
            }

            if (candidates.Data.Count == 0)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail("REFERENCE_CURVE_REQUIRED");
            }

            OperationResponse<SurfaceReferenceCurveSpec?> reference = ResolveReferenceCurve(
                document,
                candidates.Data,
                spec);
            if (!reference.Success)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail(reference.Message);
            }

            SurfaceReferenceAmbiguity ambiguity = CreateAmbiguity(candidates.Data, reference.Data, spec);
            SurfaceRebuildRouteKind route = loop.Data.Loop.Topology == SurfaceTopologyKind.Quad && loop.Data.Loop.Vertices3d.Count == 4
                ? SurfaceRebuildRouteKind.FourPoint
                : SurfaceRebuildRouteKind.BoundarySurface;

            return OperationResponse<SurfaceRebuildDescriptor>.Ok(new SurfaceRebuildDescriptor
            {
                ObjectId = objectId,
                GeometryKind = loop.Data.GeometryKind,
                CandidateReferenceCurves = candidates.Data,
                SuggestedReferenceCurve = reference.Data,
                OuterBoundaryLoop = loop.Data.Loop,
                LocalFrame = localFrame.Data,
                SuggestedDirection = SurfacePointOrderDirection.Clockwise,
                SuggestedStartAnchor = SurfacePointOrderStartAnchorMode.ReferenceStart,
                SuggestedRoute = route,
                Topology = loop.Data.Loop.Topology,
                PreserveBrepType = loop.Data.PreserveBrepType,
                Ambiguity = ambiguity,
                Warnings = CreateDescriptorWarnings(loop.Data.PreserveBrepType, ambiguity)
            });
        });
    }

    private static OperationResponse<(SurfaceBoundaryLoop Loop, string GeometryKind, bool PreserveBrepType)> ExtractBoundaryLoop(
        GeometryBase geometry,
        double tolerance)
    {
        return geometry switch
        {
            Surface surface => OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Ok((
                CreateSurfaceLoop(surface, tolerance, GetSurfaceFrontNormal(surface)),
                surface.GetType().Name,
                false)),
            Brep brep => ExtractBrepLoop(brep, tolerance),
            _ => OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Fail($"EDITABLE_KIND_UNSUPPORTED: {geometry.GetType().Name}")
        };
    }

    private static OperationResponse<(SurfaceBoundaryLoop Loop, string GeometryKind, bool PreserveBrepType)> ExtractBrepLoop(
        Brep brep,
        double tolerance)
    {
        if (brep.Faces.Count != 1)
        {
            return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Fail($"EDITABLE_KIND_UNSUPPORTED: polysurface has {brep.Faces.Count} faces");
        }

        BrepFace face = brep.Faces[0];
        GeometryVectorData frontNormal = GetBrepFaceFrontNormal(face);
        if (face.IsSurface && face.UnderlyingSurface() is Surface surface)
        {
            SurfaceBoundaryLoop surfaceLoop = CreateSurfaceLoop(surface, tolerance, frontNormal);
            return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Ok((surfaceLoop, nameof(Brep), true));
        }

        Curve? outerCurve = face.OuterLoop?.To3dCurve();
        if (outerCurve is null || !outerCurve.TryGetPolyline(out Polyline polyline))
        {
            return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Fail("EDITABLE_KIND_UNSUPPORTED: brep outer loop is not a polyline");
        }

        List<Point3d> points = NormalizePolyline(polyline);
        if (points.Count < 3)
        {
            return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Fail("INSUFFICIENT_ORDERABLE_POINTS");
        }

        bool hasInnerLoops = face.Loops.Count > 1;
        SurfaceBoundaryLoop loop = CreateLoop(points, tolerance, hasInnerLoops, frontNormal);
        return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Ok((loop, nameof(Brep), true));
    }

    private static SurfaceBoundaryLoop CreateSurfaceLoop(
        Surface surface,
        double tolerance,
        GeometryVectorData frontNormal)
    {
        var points = new List<Point3d>
        {
            surface.PointAt(surface.Domain(0).T0, surface.Domain(1).T0),
            surface.PointAt(surface.Domain(0).T1, surface.Domain(1).T0),
            surface.PointAt(surface.Domain(0).T1, surface.Domain(1).T1),
            surface.PointAt(surface.Domain(0).T0, surface.Domain(1).T1)
        };

        return CreateLoop(points, tolerance, false, frontNormal);
    }

    private static SurfaceBoundaryLoop CreateLoop(
        IReadOnlyList<Point3d> points,
        double tolerance,
        bool hasInnerLoops,
        GeometryVectorData frontNormal)
    {
        bool planar = IsPlanar(points, tolerance);
        SurfaceTopologyKind topology = points.Count == 4
            ? SurfaceTopologyKind.Quad
            : points.Count > 4 ? SurfaceTopologyKind.NGon : SurfaceTopologyKind.Unknown;
        GeometryVectorData normalizedFrontNormal = Normalize(frontNormal);
        if (IsZero(normalizedFrontNormal))
        {
            normalizedFrontNormal = LiveGeometryAnalysisHelpers.ToVectorData(ComputeNormal(points));
        }

        return new SurfaceBoundaryLoop
        {
            Vertices3d = points.Select(LiveGeometryAnalysisHelpers.ToPointData).ToList(),
            FrontNormal = normalizedFrontNormal,
            IsClosed = true,
            IsPlanar = planar,
            HasInnerLoops = hasInnerLoops,
            Topology = topology,
            Tolerance = tolerance
        };
    }

    private static List<Point3d> NormalizePolyline(Polyline polyline)
    {
        var points = polyline.ToList();
        if (points.Count > 1 && points[0].DistanceTo(points[^1]) <= 1e-9)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static OperationResponse<List<SurfaceReferenceCurveSpec>> CreateCandidates(
        SurfaceBoundaryLoop loop,
        SurfaceLocalCoordinateSystem localFrame)
    {
        IReadOnlyList<GeometryPointData> points = loop.Vertices3d;
        if (points.Count < 3)
        {
            return OperationResponse<List<SurfaceReferenceCurveSpec>>.Fail("INSUFFICIENT_ORDERABLE_POINTS");
        }

        if (localFrame.IsDegenerate)
        {
            return OperationResponse<List<SurfaceReferenceCurveSpec>>.Fail("DEGENERATE_LOCAL_FRAME");
        }

        var projected = new List<(int Index, GeometryPointData Point, GeometryPoint2dData Uv, double Angle)>();
        for (int i = 0; i < points.Count; i++)
        {
            GeometryPoint2dData uv = Project(points[i], localFrame);
            projected.Add((i, points[i], uv, Math.Atan2(uv.Y, uv.X)));
        }

        double minX = projected.Min(point => point.Uv.X);
        double maxX = projected.Max(point => point.Uv.X);
        double minY = projected.Min(point => point.Uv.Y);
        double maxY = projected.Max(point => point.Uv.Y);
        double rangeX = maxX - minX;
        double rangeY = maxY - minY;
        if (rangeX <= LocalTolerance || rangeY <= LocalTolerance)
        {
            return OperationResponse<List<SurfaceReferenceCurveSpec>>.Fail("SURFACE_LOCAL_BOUNDARY_DEGENERATE");
        }

        List<int> clockwiseIndices = projected
            .OrderByDescending(point => point.Angle)
            .ThenBy(point => Distance2d(point.Uv))
            .Select(point => point.Index)
            .ToList();

        var candidates = new List<SurfaceReferenceCurveSpec>(points.Count);
        foreach ((int index, GeometryPointData point, GeometryPoint2dData uv, double _) in projected)
        {
            int positionInClockwiseOrder = clockwiseIndices.IndexOf(index);
            int nextIndex = clockwiseIndices[(positionInClockwiseOrder + 1) % clockwiseIndices.Count];
            GeometryPointData end = points[nextIndex];
            double length = Distance(point, end);
            double normalizedX = (uv.X - minX) / rangeX;
            double normalizedY = (uv.Y - minY) / rangeY;
            double score = Math.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
            (int? edgeIndex, bool reversed) = ResolveBoundaryEdgeIndex(index, nextIndex, points.Count);
            candidates.Add(new SurfaceReferenceCurveSpec
            {
                EdgeIndex = edgeIndex,
                StartVertexIndex = index,
                EndVertexIndex = nextIndex,
                ReversedFromBoundary = reversed,
                StartPoint = point,
                EndPoint = end,
                Midpoint = new GeometryPointData
                {
                    X = (point.X + end.X) * 0.5d,
                    Y = (point.Y + end.Y) * 0.5d,
                    Z = (point.Z + end.Z) * 0.5d
                },
                Length = length,
                Score = score
            });
        }

        return OperationResponse<List<SurfaceReferenceCurveSpec>>.Ok(candidates
            .OrderBy(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Length)
            .ToList());
    }

    private static OperationResponse<SurfaceReferenceCurveSpec?> ResolveReferenceCurve(
        rhinocommon::Rhino.RhinoDoc document,
        IReadOnlyList<SurfaceReferenceCurveSpec> candidates,
        SurfaceRebuildSpec spec)
    {
        if (spec.GuideMode == SurfacePointOrderGuideMode.ReferenceCurve)
        {
            if (!spec.ReferenceCurveObjectId.HasValue)
            {
                return OperationResponse<SurfaceReferenceCurveSpec?>.Fail("REFERENCE_CURVE_REQUIRED");
            }

            var referenceObject = document.Objects.FindId(spec.ReferenceCurveObjectId.Value);
            if (referenceObject?.Geometry is not Curve curve)
            {
                return OperationResponse<SurfaceReferenceCurveSpec?>.Fail("REFERENCE_CURVE_REQUIRED");
            }

            Point3d start = curve.PointAtStart;
            Point3d end = curve.PointAtEnd;
            return OperationResponse<SurfaceReferenceCurveSpec?>.Ok(new SurfaceReferenceCurveSpec
            {
                ObjectId = spec.ReferenceCurveObjectId,
                StartPoint = LiveGeometryAnalysisHelpers.ToPointData(start),
                EndPoint = LiveGeometryAnalysisHelpers.ToPointData(end),
                Midpoint = LiveGeometryAnalysisHelpers.ToPointData(curve.PointAtNormalizedLength(0.5d)),
                Length = curve.GetLength()
            });
        }

        if (spec.GuideMode == SurfacePointOrderGuideMode.ReferenceEdgeIndex)
        {
            if (!spec.ReferenceEdgeIndex.HasValue)
            {
                return OperationResponse<SurfaceReferenceCurveSpec?>.Fail("REFERENCE_CURVE_REQUIRED");
            }

            SurfaceReferenceCurveSpec? match = candidates.FirstOrDefault(candidate => candidate.EdgeIndex == spec.ReferenceEdgeIndex.Value);
            return match is null
                ? OperationResponse<SurfaceReferenceCurveSpec?>.Fail("REFERENCE_CURVE_REQUIRED")
                : OperationResponse<SurfaceReferenceCurveSpec?>.Ok(match);
        }

        SurfaceReferenceCurveSpec first = candidates[0];
        bool ambiguous = candidates.Count > 1
            && Math.Abs(first.Score - candidates[1].Score) <= LocalTolerance;

        return OperationResponse<SurfaceReferenceCurveSpec?>.Ok(ambiguous ? null : first);
    }

    private static SurfaceReferenceAmbiguity CreateAmbiguity(
        IReadOnlyList<SurfaceReferenceCurveSpec> candidates,
        SurfaceReferenceCurveSpec? selected,
        SurfaceRebuildSpec spec)
    {
        if (selected is null)
        {
            return new SurfaceReferenceAmbiguity
            {
                Level = "High",
                Reason = "Multiple reference edges have equivalent automatic ranking."
            };
        }

        if (spec.GuideMode != SurfacePointOrderGuideMode.Auto || candidates.Count < 2)
        {
            return new SurfaceReferenceAmbiguity { Level = "None" };
        }

        double scoreGap = Math.Abs(candidates[0].Score - candidates[1].Score);
        return scoreGap < 1e-3
            ? new SurfaceReferenceAmbiguity { Level = "Low", Reason = "The next lower-left candidate is close to the selected automatic candidate in the surface-local gravity frame." }
            : new SurfaceReferenceAmbiguity { Level = "None" };
    }

    private static IReadOnlyList<string> CreateDescriptorWarnings(
        bool preserveBrepType,
        SurfaceReferenceAmbiguity ambiguity)
    {
        var warnings = new List<string>();
        if (preserveBrepType)
        {
            warnings.Add("UNDERLYING_SURFACE_FALLBACK");
        }

        if (string.Equals(ambiguity.Level, "Low", StringComparison.Ordinal))
        {
            warnings.Add("REFERENCE_CURVE_AUTO_LOW_CONFIDENCE");
        }

        return warnings;
    }

    private static GeometryVectorData GetSurfaceFrontNormal(Surface surface)
    {
        double u = surface.Domain(0).Mid;
        double v = surface.Domain(1).Mid;
        Vector3d normal = surface.NormalAt(u, v);
        if (!normal.Unitize())
        {
            return new GeometryVectorData();
        }

        return LiveGeometryAnalysisHelpers.ToVectorData(normal);
    }

    private static GeometryVectorData GetBrepFaceFrontNormal(BrepFace face)
    {
        double u = face.Domain(0).Mid;
        double v = face.Domain(1).Mid;
        Vector3d normal = face.NormalAt(u, v);
        if (!normal.Unitize())
        {
            return new GeometryVectorData();
        }

        if (face.OrientationIsReversed)
        {
            normal = new Vector3d(-normal.X, -normal.Y, -normal.Z);
        }

        return LiveGeometryAnalysisHelpers.ToVectorData(normal);
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

    private static (int? EdgeIndex, bool Reversed) ResolveBoundaryEdgeIndex(
        int startIndex,
        int endIndex,
        int pointCount)
    {
        if (endIndex == (startIndex + 1) % pointCount)
        {
            return (startIndex, false);
        }

        if (startIndex == (endIndex + 1) % pointCount)
        {
            return (endIndex, true);
        }

        return (null, false);
    }

    private static bool IsPlanar(IReadOnlyList<Point3d> points, double tolerance)
    {
        Vector3d normal = ComputeNormal(points);
        if (!normal.Unitize())
        {
            return false;
        }

        Point3d origin = points[0];
        double allowed = Math.Max(tolerance, 1e-6);
        foreach (Point3d point in points)
        {
            Vector3d delta = point - origin;
            if (Math.Abs(delta * normal) > allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector3d ComputeNormal(IReadOnlyList<Point3d> points)
    {
        double x = 0d;
        double y = 0d;
        double z = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            Point3d current = points[i];
            Point3d next = points[(i + 1) % points.Count];
            x += (current.Y - next.Y) * (current.Z + next.Z);
            y += (current.Z - next.Z) * (current.X + next.X);
            z += (current.X - next.X) * (current.Y + next.Y);
        }

        return new Vector3d(x, y, z);
    }

    private static GeometryVectorData Normalize(GeometryVectorData vector)
    {
        double length = Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        if (length <= LocalTolerance || double.IsNaN(length) || double.IsInfinity(length))
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
        return Math.Abs(vector.X) <= LocalTolerance
            && Math.Abs(vector.Y) <= LocalTolerance
            && Math.Abs(vector.Z) <= LocalTolerance;
    }

    private static double Distance(GeometryPointData a, GeometryPointData b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static double Distance2d(GeometryPoint2dData point)
    {
        return Math.Sqrt((point.X * point.X) + (point.Y * point.Y));
    }

    private static double Dot(double x, double y, double z, GeometryVectorData axis)
    {
        return (x * axis.X) + (y * axis.Y) + (z * axis.Z);
    }
}
