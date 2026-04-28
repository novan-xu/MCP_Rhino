extern alias rhinocommon;

using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Contracts.Requests;
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
using PlaneSurface = rhinocommon::Rhino.Geometry.PlaneSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;
using PolylineCurve = rhinocommon::Rhino.Geometry.PolylineCurve;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using Surface = rhinocommon::Rhino.Geometry.Surface;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveEditableGeometryDescriptorService : IEditableGeometryDescriptorService
{
    private const int LargeFullSurfacePointThreshold = 10000;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly IGeometryMetadataOperator _metadataOperator;
    private readonly IBrepSurfaceDowngrader _brepSurfaceDowngrader;

    public LiveEditableGeometryDescriptorService(
        ILiveRhinoDocumentAccessor documentAccessor,
        IGeometryMetadataOperator metadataOperator,
        IBrepSurfaceDowngrader brepSurfaceDowngrader)
    {
        _documentAccessor = documentAccessor;
        _metadataOperator = metadataOperator;
        _brepSurfaceDowngrader = brepSurfaceDowngrader;
    }

    public OperationResponse<EditableGeometryDescriptorResponse> Read(GetEditableGeometryDescriptorRequest request)
    {
        if (request.ObjectId == Guid.Empty)
        {
            return OperationResponse<EditableGeometryDescriptorResponse>.Fail("ObjectId cannot be empty.");
        }

        return _documentAccessor.Execute(request.FilePath, document =>
        {
            RhinoObject? rhinoObject = document.Objects.FindId(request.ObjectId);
            if (rhinoObject?.Geometry is null)
            {
                return OperationResponse<EditableGeometryDescriptorResponse>.Fail($"OBJECT_NOT_FOUND: {request.ObjectId}");
            }

            OperationResponse<EditableGeometryDescriptor> descriptor = BuildDescriptor(
                document,
                rhinoObject,
                request.Detail);
            if (!descriptor.Success || descriptor.Data is null)
            {
                return OperationResponse<EditableGeometryDescriptorResponse>.Fail(descriptor.Message);
            }

            return OperationResponse<EditableGeometryDescriptorResponse>.Ok(new EditableGeometryDescriptorResponse
            {
                FilePath = request.FilePath,
                ObjectId = request.ObjectId,
                Descriptor = descriptor.Data
            });
        });
    }

    private OperationResponse<EditableGeometryDescriptor> BuildDescriptor(
        rhinocommon::Rhino.RhinoDoc document,
        RhinoObject rhinoObject,
        DescriptorDetail? requestedDetail)
    {
        GeometryBase geometry = rhinoObject.Geometry;
        return geometry switch
        {
            Curve curve => BuildCurveDescriptor(document, rhinoObject, curve, requestedDetail ?? DescriptorDetail.Full),
            Surface surface => BuildSurfaceDescriptor(document, rhinoObject, surface, requestedDetail ?? DescriptorDetail.Summary, Array.Empty<string>()),
            Brep => BuildBrepSurfaceDescriptor(document, rhinoObject, requestedDetail ?? DescriptorDetail.Summary),
            _ => OperationResponse<EditableGeometryDescriptor>.Fail($"EDITABLE_KIND_UNSUPPORTED: {geometry.GetType().Name}")
        };
    }

    private OperationResponse<EditableGeometryDescriptor> BuildCurveDescriptor(
        rhinocommon::Rhino.RhinoDoc document,
        RhinoObject rhinoObject,
        Curve curve,
        DescriptorDetail detail)
    {
        OperationResponse<(EditableCurveStructure Structure, List<Point3d> Points)> structure = GetCurveStructureAndPoints(curve);
        if (!structure.Success)
        {
            return OperationResponse<EditableGeometryDescriptor>.Fail(structure.Message);
        }

        (EditableCurveStructure curveStructure, List<Point3d> controlPoints) = structure.Data;
        var pointDescriptors = detail == DescriptorDetail.Full
            ? ToPointDescriptors(controlPoints)
            : Array.Empty<EditablePointDescriptor>();

        return OperationResponse<EditableGeometryDescriptor>.Ok(new EditableGeometryDescriptor
        {
            ObjectId = rhinoObject.Attributes.ObjectId,
            Kind = EditableGeometryKind.Curve,
            Detail = detail,
            CurveStructure = curveStructure,
            ControlPointCentroidWorld = ToPointData(ComputeCentroid(controlPoints)),
            ControlPointCount = controlPoints.Count,
            Points = pointDescriptors,
            MetadataSummary = _metadataOperator.Summarize(document, rhinoObject),
            SupportKind = GeometryReconstructionSupportKind.Supported
        });
    }

    private OperationResponse<EditableGeometryDescriptor> BuildBrepSurfaceDescriptor(
        rhinocommon::Rhino.RhinoDoc document,
        RhinoObject rhinoObject,
        DescriptorDetail detail)
    {
        OperationResponse<(BrepDowngradeResult Result, Surface? Surface)> downgrade = _brepSurfaceDowngrader.Evaluate(
            rhinoObject,
            document.ModelAbsoluteTolerance);
        if (!downgrade.Success || downgrade.Data.Surface is null)
        {
            string reason = downgrade.Data.Result?.Reason ?? downgrade.Message;
            return OperationResponse<EditableGeometryDescriptor>.Fail($"EDITABLE_KIND_UNSUPPORTED: {reason}");
        }

        if (!downgrade.Data.Result.IsUntrimmedSingleFaceBrep)
        {
            return OperationResponse<EditableGeometryDescriptor>.Fail($"EDITABLE_KIND_UNSUPPORTED: {downgrade.Data.Result.Reason}");
        }

        return BuildSurfaceDescriptor(
            document,
            rhinoObject,
            downgrade.Data.Surface,
            detail,
            new[] { "UNDERLYING_SURFACE_FALLBACK" });
    }

    private OperationResponse<EditableGeometryDescriptor> BuildSurfaceDescriptor(
        rhinocommon::Rhino.RhinoDoc document,
        RhinoObject rhinoObject,
        Surface surface,
        DescriptorDetail detail,
        IReadOnlyList<string> initialWarnings)
    {
        if (surface is not PlaneSurface && surface is not NurbsSurface)
        {
            return OperationResponse<EditableGeometryDescriptor>.Fail($"EDITABLE_KIND_UNSUPPORTED: {surface.GetType().Name}");
        }

        using NurbsSurface? nurbsSurface = surface.ToNurbsSurface();
        if (nurbsSurface is null)
        {
            return OperationResponse<EditableGeometryDescriptor>.Fail($"EDITABLE_KIND_UNSUPPORTED: {surface.GetType().Name} cannot convert to NurbsSurface");
        }

        List<Point3d> controlPoints = GetSurfaceControlPoints(nurbsSurface);
        var warnings = new List<string>(initialWarnings);
        if (detail == DescriptorDetail.Full && controlPoints.Count > LargeFullSurfacePointThreshold)
        {
            warnings.Add("DESCRIPTOR_FULL_LARGE");
        }

        IReadOnlyList<EditablePointDescriptor> pointDescriptors = detail == DescriptorDetail.Full
            ? ToSurfacePointDescriptors(nurbsSurface)
            : Array.Empty<EditablePointDescriptor>();

        BoundingBox boundingBox = surface.GetBoundingBox(true);
        return OperationResponse<EditableGeometryDescriptor>.Ok(new EditableGeometryDescriptor
        {
            ObjectId = rhinoObject.Attributes.ObjectId,
            Kind = EditableGeometryKind.Surface,
            Detail = detail,
            SurfaceStructure = new EditableSurfaceStructure
            {
                SurfaceKind = surface.GetType().Name,
                DegreeU = Math.Max(0, nurbsSurface.OrderU - 1),
                DegreeV = Math.Max(0, nurbsSurface.OrderV - 1),
                CountU = nurbsSurface.Points.CountU,
                CountV = nurbsSurface.Points.CountV,
                IsClosedU = surface.IsClosed(0),
                IsClosedV = surface.IsClosed(1),
                IsPeriodicU = surface.IsPeriodic(0),
                IsPeriodicV = surface.IsPeriodic(1),
                BoundingBox = new GeometryBoundingBoxData
                {
                    Min = ToPointData(boundingBox.Min),
                    Max = ToPointData(boundingBox.Max)
                }
            },
            ControlPointCentroidWorld = ToPointData(ComputeCentroid(controlPoints)),
            ControlPointCount = controlPoints.Count,
            Points = pointDescriptors,
            MetadataSummary = _metadataOperator.Summarize(document, rhinoObject),
            SupportKind = GeometryReconstructionSupportKind.Supported,
            Warnings = warnings
        });
    }

    private static OperationResponse<(EditableCurveStructure Structure, List<Point3d> Points)> GetCurveStructureAndPoints(Curve curve)
    {
        switch (curve)
        {
            case LineCurve lineCurve:
                return OperationResponse<(EditableCurveStructure, List<Point3d>)>.Ok((
                    new EditableCurveStructure
                    {
                        CurveKind = nameof(LineCurve),
                        Degree = 1,
                        IsClosed = lineCurve.IsClosed,
                        IsPeriodic = lineCurve.IsPeriodic
                    },
                    new List<Point3d> { lineCurve.PointAtStart, lineCurve.PointAtEnd }));

            case PolylineCurve polylineCurve when polylineCurve.TryGetPolyline(out Polyline polyline):
                return OperationResponse<(EditableCurveStructure, List<Point3d>)>.Ok((
                    new EditableCurveStructure
                    {
                        CurveKind = nameof(PolylineCurve),
                        Degree = 1,
                        IsClosed = polylineCurve.IsClosed,
                        IsPeriodic = polylineCurve.IsPeriodic
                    },
                    polyline.ToList()));

            case NurbsCurve nurbsCurve:
                var points = new List<Point3d>(nurbsCurve.Points.Count);
                for (int i = 0; i < nurbsCurve.Points.Count; i++)
                {
                    points.Add(nurbsCurve.Points[i].Location);
                }

                return OperationResponse<(EditableCurveStructure, List<Point3d>)>.Ok((
                    new EditableCurveStructure
                    {
                        CurveKind = nameof(NurbsCurve),
                        Degree = nurbsCurve.Degree,
                        IsClosed = nurbsCurve.IsClosed,
                        IsPeriodic = nurbsCurve.IsPeriodic
                    },
                    points));

            default:
                return OperationResponse<(EditableCurveStructure, List<Point3d>)>.Fail($"EDITABLE_KIND_UNSUPPORTED: {curve.GetType().Name}");
        }
    }

    private static List<Point3d> GetSurfaceControlPoints(NurbsSurface nurbsSurface)
    {
        var points = new List<Point3d>(nurbsSurface.Points.CountU * nurbsSurface.Points.CountV);
        for (int u = 0; u < nurbsSurface.Points.CountU; u++)
        {
            for (int v = 0; v < nurbsSurface.Points.CountV; v++)
            {
                points.Add(nurbsSurface.Points.GetControlPoint(u, v).Location);
            }
        }

        return points;
    }

    private static IReadOnlyList<EditablePointDescriptor> ToPointDescriptors(IReadOnlyList<Point3d> points)
    {
        var descriptors = new List<EditablePointDescriptor>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            Point3d point = points[i];
            descriptors.Add(new EditablePointDescriptor
            {
                Index = i,
                X = point.X,
                Y = point.Y,
                Z = point.Z
            });
        }

        return descriptors;
    }

    private static IReadOnlyList<EditablePointDescriptor> ToSurfacePointDescriptors(NurbsSurface nurbsSurface)
    {
        var descriptors = new List<EditablePointDescriptor>(nurbsSurface.Points.CountU * nurbsSurface.Points.CountV);
        for (int u = 0; u < nurbsSurface.Points.CountU; u++)
        {
            for (int v = 0; v < nurbsSurface.Points.CountV; v++)
            {
                Point3d point = nurbsSurface.Points.GetControlPoint(u, v).Location;
                descriptors.Add(new EditablePointDescriptor
                {
                    Index = (u * nurbsSurface.Points.CountV) + v,
                    UIndex = u,
                    VIndex = v,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z
                });
            }
        }

        return descriptors;
    }

    private static Point3d ComputeCentroid(IReadOnlyList<Point3d> points)
    {
        if (points.Count == 0)
        {
            return Point3d.Origin;
        }

        double x = 0d;
        double y = 0d;
        double z = 0d;
        foreach (Point3d point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        return new Point3d(x / points.Count, y / points.Count, z / points.Count);
    }

    private static GeometryPointData ToPointData(Point3d point)
    {
        return LiveGeometryAnalysisHelpers.ToPointData(point);
    }
}
