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
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveBoundaryReferenceCurveAnalyzer(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
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

            List<SurfaceReferenceCurveSpec> candidates = CreateCandidates(loop.Data.Loop);
            if (candidates.Count == 0)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail("REFERENCE_CURVE_REQUIRED");
            }

            OperationResponse<SurfaceReferenceCurveSpec?> reference = ResolveReferenceCurve(
                document,
                candidates,
                spec);
            if (!reference.Success)
            {
                return OperationResponse<SurfaceRebuildDescriptor>.Fail(reference.Message);
            }

            SurfaceReferenceAmbiguity ambiguity = CreateAmbiguity(candidates, reference.Data, spec);
            SurfaceRebuildRouteKind route = loop.Data.Loop.Topology == SurfaceTopologyKind.Quad && loop.Data.Loop.Vertices3d.Count == 4
                ? SurfaceRebuildRouteKind.FourPoint
                : SurfaceRebuildRouteKind.BoundarySurface;

            return OperationResponse<SurfaceRebuildDescriptor>.Ok(new SurfaceRebuildDescriptor
            {
                ObjectId = objectId,
                GeometryKind = loop.Data.GeometryKind,
                CandidateReferenceCurves = candidates,
                SuggestedReferenceCurve = reference.Data,
                OuterBoundaryLoop = loop.Data.Loop,
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
                CreateSurfaceLoop(surface, tolerance),
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
        if (face.IsSurface && face.UnderlyingSurface() is Surface surface)
        {
            SurfaceBoundaryLoop surfaceLoop = CreateSurfaceLoop(surface, tolerance);
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
        SurfaceBoundaryLoop loop = CreateLoop(points, tolerance, hasInnerLoops);
        return OperationResponse<(SurfaceBoundaryLoop, string, bool)>.Ok((loop, nameof(Brep), true));
    }

    private static SurfaceBoundaryLoop CreateSurfaceLoop(Surface surface, double tolerance)
    {
        var points = new List<Point3d>
        {
            surface.PointAt(surface.Domain(0).T0, surface.Domain(1).T0),
            surface.PointAt(surface.Domain(0).T1, surface.Domain(1).T0),
            surface.PointAt(surface.Domain(0).T1, surface.Domain(1).T1),
            surface.PointAt(surface.Domain(0).T0, surface.Domain(1).T1)
        };

        return CreateLoop(points, tolerance, false);
    }

    private static SurfaceBoundaryLoop CreateLoop(IReadOnlyList<Point3d> points, double tolerance, bool hasInnerLoops)
    {
        bool planar = IsPlanar(points, tolerance);
        SurfaceTopologyKind topology = points.Count == 4
            ? SurfaceTopologyKind.Quad
            : points.Count > 4 ? SurfaceTopologyKind.NGon : SurfaceTopologyKind.Unknown;
        return new SurfaceBoundaryLoop
        {
            Vertices3d = points.Select(LiveGeometryAnalysisHelpers.ToPointData).ToList(),
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

    private static List<SurfaceReferenceCurveSpec> CreateCandidates(SurfaceBoundaryLoop loop)
    {
        var candidates = new List<SurfaceReferenceCurveSpec>();
        IReadOnlyList<GeometryPointData> points = loop.Vertices3d;
        for (int i = 0; i < points.Count; i++)
        {
            GeometryPointData start = points[i];
            GeometryPointData end = points[(i + 1) % points.Count];
            double length = Distance(start, end);
            double score = ((start.Y + end.Y) * 0.5d) + (((start.Z + end.Z) * 0.5d) * 0.001d);
            candidates.Add(new SurfaceReferenceCurveSpec
            {
                EdgeIndex = i,
                StartPoint = start,
                EndPoint = end,
                Midpoint = new GeometryPointData
                {
                    X = (start.X + end.X) * 0.5d,
                    Y = (start.Y + end.Y) * 0.5d,
                    Z = (start.Z + end.Z) * 0.5d
                },
                Length = length,
                Score = score
            });
        }

        return candidates
            .OrderBy(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Length)
            .ToList();
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
            && Math.Abs(first.Score - candidates[1].Score) <= 1e-9
            && Math.Abs(first.Length - candidates[1].Length) <= 1e-9;

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
            ? new SurfaceReferenceAmbiguity { Level = "Low", Reason = "The next reference edge is close to the selected automatic candidate." }
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

    private static double Distance(GeometryPointData a, GeometryPointData b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
