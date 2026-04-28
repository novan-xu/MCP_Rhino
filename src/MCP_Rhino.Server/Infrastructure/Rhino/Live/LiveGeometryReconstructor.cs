extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using LineCurve = rhinocommon::Rhino.Geometry.LineCurve;
using NurbsCurve = rhinocommon::Rhino.Geometry.NurbsCurve;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveGeometryReconstructor : IGeometryReconstructor
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveGeometryReconstructor(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<CurveReconstructionResult> ReconstructCurve(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Curve curve)
            {
                return OperationResponse<CurveReconstructionResult>.Fail("EDITABLE_KIND_UNSUPPORTED");
            }

            return Reconstruct(curve, descriptor, points);
        });
    }

    public OperationResponse<SurfaceReconstructionResult> ReconstructSurface(
        string filePath,
        Guid objectId,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points)
    {
        return _documentAccessor.Execute(filePath, document =>
        {
            var rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is null)
            {
                return OperationResponse<SurfaceReconstructionResult>.Fail($"OBJECT_NOT_FOUND: {objectId}");
            }

            OperationResponse<(Surface Surface, bool WrapAsBrep, string SourceKind)> source = ResolveSourceSurface(rhinoObject.Geometry);
            if (!source.Success)
            {
                return OperationResponse<SurfaceReconstructionResult>.Fail(source.Message);
            }

            return ReconstructSurfaceGeometry(source.Data.Surface, source.Data.WrapAsBrep, source.Data.SourceKind, descriptor, points);
        });
    }

    private static OperationResponse<CurveReconstructionResult> Reconstruct(
        Curve sourceCurve,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points)
    {
        List<Point3d> pointList = points.Select(point => new Point3d(point.X, point.Y, point.Z)).ToList();
        var warnings = new List<ObjectEditWarning>();
        GeometryEditStrategyKind strategy;
        Curve reconstructed;

        switch (sourceCurve)
        {
            case LineCurve:
                if (pointList.Count != 2)
                {
                    return OperationResponse<CurveReconstructionResult>.Fail("EDIT_POINT_COUNT_MISMATCH");
                }

                strategy = GeometryEditStrategyKind.ReconstructFromControlPoints;
                reconstructed = new LineCurve(pointList[0], pointList[1]);
                break;

            case PolylineCurve:
                strategy = GeometryEditStrategyKind.ReconstructFromPoints;
                reconstructed = new PolylineCurve(pointList);
                bool wasClosed = descriptor.CurveStructure?.IsClosed == true;
                bool isClosed = pointList.Count > 1 && pointList[0].DistanceTo(pointList[^1]) <= 1e-9;
                if (wasClosed != isClosed)
                {
                    warnings.Add(new ObjectEditWarning
                    {
                        Code = "POLYLINE_CLOSURE_CHANGED",
                        Message = "The edited polyline closure state differs from the source descriptor."
                    });
                }
                break;

            case NurbsCurve nurbsCurve:
                strategy = GeometryEditStrategyKind.ReconstructFromControlPoints;
                if (pointList.Count != nurbsCurve.Points.Count)
                {
                    return OperationResponse<CurveReconstructionResult>.Fail("EDIT_POINT_COUNT_MISMATCH");
                }

                if (nurbsCurve.DuplicateCurve() is not NurbsCurve duplicate)
                {
                    return OperationResponse<CurveReconstructionResult>.Fail("RECONSTRUCTION_INVALID");
                }

                for (int i = 0; i < pointList.Count; i++)
                {
                    if (!duplicate.Points.SetPoint(i, pointList[i]))
                    {
                        return OperationResponse<CurveReconstructionResult>.Fail("RECONSTRUCTION_INVALID");
                    }
                }

                reconstructed = duplicate;
                break;

            default:
                return OperationResponse<CurveReconstructionResult>.Fail($"EDITABLE_KIND_UNSUPPORTED: {sourceCurve.GetType().Name}");
        }

        ReconstructedCurveSummary summary = CreateSummary(reconstructed, pointList.Count);
        if (!summary.IsValid)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_INVALID",
                Message = "The reconstructed curve is not valid."
            });
        }

        return OperationResponse<CurveReconstructionResult>.Ok(new CurveReconstructionResult
        {
            Strategy = strategy,
            Curve = reconstructed,
            Summary = summary,
            Warnings = warnings
        });
    }

    private static ReconstructedCurveSummary CreateSummary(Curve curve, int pointCount)
    {
        BoundingBox boundingBox = curve.GetBoundingBox(true);
        return new ReconstructedCurveSummary
        {
            CurveKind = curve.GetType().Name,
            Degree = curve is NurbsCurve nurbsCurve ? nurbsCurve.Degree : 1,
            IsClosed = curve.IsClosed,
            IsValid = curve.IsValid,
            PointCount = pointCount,
            BoundingBox = new GeometryBoundingBoxData
            {
                Min = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Min),
                Max = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Max)
            }
        };
    }

    private static OperationResponse<(Surface Surface, bool WrapAsBrep, string SourceKind)> ResolveSourceSurface(GeometryBase geometry)
    {
        switch (geometry)
        {
            case Surface surface:
                return OperationResponse<(Surface, bool, string)>.Ok((surface, false, surface.GetType().Name));
            case Brep brep when brep.Faces.Count == 1 && brep.Faces[0].IsSurface:
                Surface? underlyingSurface = brep.Faces[0].UnderlyingSurface();
                return underlyingSurface is null
                    ? OperationResponse<(Surface, bool, string)>.Fail("EDITABLE_KIND_UNSUPPORTED: underlying surface is missing")
                    : OperationResponse<(Surface, bool, string)>.Ok((underlyingSurface, true, nameof(Brep)));
            default:
                return OperationResponse<(Surface, bool, string)>.Fail($"EDITABLE_KIND_UNSUPPORTED: {geometry.GetType().Name}");
        }
    }

    private static OperationResponse<SurfaceReconstructionResult> ReconstructSurfaceGeometry(
        Surface sourceSurface,
        bool wrapAsBrep,
        string sourceKind,
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points)
    {
        if (descriptor.SurfaceStructure is null)
        {
            return OperationResponse<SurfaceReconstructionResult>.Fail("EDITABLE_KIND_UNSUPPORTED");
        }

        int countU = descriptor.SurfaceStructure.CountU;
        int countV = descriptor.SurfaceStructure.CountV;
        if (points.Count != countU * countV)
        {
            return OperationResponse<SurfaceReconstructionResult>.Fail("EDIT_POINT_COUNT_MISMATCH");
        }

        NurbsSurface? reconstructed = sourceSurface.ToNurbsSurface();
        if (reconstructed is null)
        {
            return OperationResponse<SurfaceReconstructionResult>.Fail("RECONSTRUCTION_INVALID");
        }

        if (reconstructed.Points.CountU != countU || reconstructed.Points.CountV != countV)
        {
            reconstructed.Dispose();
            return OperationResponse<SurfaceReconstructionResult>.Fail("EDIT_POINT_COUNT_MISMATCH");
        }

        for (int i = 0; i < points.Count; i++)
        {
            int u = i / countV;
            int v = i % countV;
            EditablePointInput point = points[i];
            if (!reconstructed.Points.SetPoint(u, v, new Point3d(point.X, point.Y, point.Z)))
            {
                reconstructed.Dispose();
                return OperationResponse<SurfaceReconstructionResult>.Fail("RECONSTRUCTION_INVALID");
            }
        }

        var warnings = new List<ObjectEditWarning>();
        AddSurfaceSeamWarnings(descriptor, points, warnings);
        if (!string.Equals(sourceKind, nameof(NurbsSurface), StringComparison.Ordinal) && !wrapAsBrep)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_SEMANTICS_CHANGED",
                Message = $"{sourceKind} was reconstructed as NurbsSurface."
            });
        }

        SurfaceControlPointGridSnapshot summary = CreateSurfaceSummary(reconstructed, countU, countV);
        GeometryBase geometry = reconstructed;
        if (wrapAsBrep)
        {
            Brep? brep = Brep.CreateFromSurface(reconstructed);
            if (brep is null)
            {
                reconstructed.Dispose();
                return OperationResponse<SurfaceReconstructionResult>.Fail("RECONSTRUCTION_INVALID");
            }

            geometry = brep;
        }

        if (!summary.IsValid)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_INVALID",
                Message = "The reconstructed surface is not valid."
            });
        }

        return OperationResponse<SurfaceReconstructionResult>.Ok(new SurfaceReconstructionResult
        {
            Strategy = GeometryEditStrategyKind.ReconstructFromControlPoints,
            Geometry = geometry,
            Summary = summary,
            Warnings = warnings
        });
    }

    private static SurfaceControlPointGridSnapshot CreateSurfaceSummary(NurbsSurface surface, int countU, int countV)
    {
        BoundingBox boundingBox = surface.GetBoundingBox(true);
        return new SurfaceControlPointGridSnapshot
        {
            SurfaceKind = surface.GetType().Name,
            CountU = countU,
            CountV = countV,
            IsValid = surface.IsValid,
            BoundingBox = new GeometryBoundingBoxData
            {
                Min = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Min),
                Max = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Max)
            },
            SampleCorners = GetSurfaceCorners(surface, countU, countV)
        };
    }

    private static IReadOnlyList<GeometryPointData> GetSurfaceCorners(NurbsSurface surface, int countU, int countV)
    {
        var corners = new List<GeometryPointData>();
        var keys = new HashSet<(int U, int V)>();
        AddCorner(0, 0);
        AddCorner(0, countV - 1);
        AddCorner(countU - 1, 0);
        AddCorner(countU - 1, countV - 1);
        return corners;

        void AddCorner(int u, int v)
        {
            if (u < 0 || v < 0 || u >= countU || v >= countV || !keys.Add((u, v)))
            {
                return;
            }

            corners.Add(LiveGeometryAnalysisHelpers.ToPointData(surface.Points.GetControlPoint(u, v).Location));
        }
    }

    private static void AddSurfaceSeamWarnings(
        EditableGeometryDescriptor descriptor,
        IReadOnlyList<EditablePointInput> points,
        List<ObjectEditWarning> warnings)
    {
        if (descriptor.SurfaceStructure is null)
        {
            return;
        }

        int countU = descriptor.SurfaceStructure.CountU;
        int countV = descriptor.SurfaceStructure.CountV;
        bool inconsistent = false;

        if ((descriptor.SurfaceStructure.IsClosedU || descriptor.SurfaceStructure.IsPeriodicU) && countU > 1)
        {
            for (int v = 0; v < countV; v++)
            {
                inconsistent |= Distance(points[v], points[((countU - 1) * countV) + v]) > 1e-6;
            }
        }

        if ((descriptor.SurfaceStructure.IsClosedV || descriptor.SurfaceStructure.IsPeriodicV) && countV > 1)
        {
            for (int u = 0; u < countU; u++)
            {
                inconsistent |= Distance(points[u * countV], points[(u * countV) + countV - 1]) > 1e-6;
            }
        }

        if (inconsistent)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "SURFACE_CLOSURE_SEAM_INCONSISTENT",
                Message = "The edited control-point grid is inconsistent across a closed or periodic surface seam."
            });
        }
    }

    private static double Distance(EditablePointInput a, EditablePointInput b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
