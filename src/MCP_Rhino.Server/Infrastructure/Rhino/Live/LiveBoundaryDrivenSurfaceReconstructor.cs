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
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Polyline = rhinocommon::Rhino.Geometry.Polyline;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveBoundaryDrivenSurfaceReconstructor : IBoundaryDrivenSurfaceReconstructor
{
    public OperationResponse<BoundaryRebuildResult> Reconstruct(
        SurfacePointOrderPlan plan,
        SurfaceBoundaryLoop boundaryLoop)
    {
        if (plan.Points.Count < 3)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("INSUFFICIENT_ORDERABLE_POINTS");
        }

        return plan.Route switch
        {
            SurfaceRebuildRouteKind.FourPoint => ReconstructFourPoint(plan),
            SurfaceRebuildRouteKind.BoundarySurface => ReconstructBoundarySurface(plan, boundaryLoop),
            _ => OperationResponse<BoundaryRebuildResult>.Fail("UNSUPPORTED_TOPOLOGY")
        };
    }

    private static OperationResponse<BoundaryRebuildResult> ReconstructFourPoint(SurfacePointOrderPlan plan)
    {
        if (plan.Points.Count != 4)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("BOUNDARY_SURFACE_FAILED: FourPoint route requires exactly four points.");
        }

        Point3d p0 = ToPoint3d(plan.Points[0].Position3d);
        Point3d p1 = ToPoint3d(plan.Points[1].Position3d);
        Point3d p2 = ToPoint3d(plan.Points[2].Position3d);
        Point3d p3 = ToPoint3d(plan.Points[3].Position3d);
        NurbsSurface? surface = NurbsSurface.CreateFromCorners(p0, p1, p2, p3);
        if (surface is null)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("BOUNDARY_SURFACE_FAILED: 4PointSurface creation failed.");
        }

        GeometryBase geometry = surface;
        if (plan.PreserveBrepType)
        {
            Brep? brep = Brep.CreateFromSurface(surface);
            if (brep is null)
            {
                return OperationResponse<BoundaryRebuildResult>.Fail("BOUNDARY_SURFACE_FAILED: Brep wrapper creation failed.");
            }

            geometry = brep;
        }

        return CreateResult(geometry, SurfaceRebuildRouteKind.FourPoint, plan.Points.Count);
    }

    private static OperationResponse<BoundaryRebuildResult> ReconstructBoundarySurface(
        SurfacePointOrderPlan plan,
        SurfaceBoundaryLoop boundaryLoop)
    {
        if (!boundaryLoop.IsPlanar)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("BOUNDARY_SURFACE_NON_PLANAR_UNSUPPORTED");
        }

        if (boundaryLoop.HasInnerLoops)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("UNSUPPORTED_TOPOLOGY: inner loops are not supported.");
        }

        var polyline = new Polyline(plan.Points.Select(point => ToPoint3d(point.Position3d)));
        polyline.Add(ToPoint3d(plan.Points[0].Position3d));
        Curve curve = polyline.ToNurbsCurve();
        Brep[]? breps = Brep.CreatePlanarBreps(curve, Math.Max(boundaryLoop.Tolerance, 1e-6));
        if (breps is null || breps.Length != 1)
        {
            return OperationResponse<BoundaryRebuildResult>.Fail("BOUNDARY_SURFACE_FAILED");
        }

        return CreateResult(breps[0], SurfaceRebuildRouteKind.BoundarySurface, plan.Points.Count);
    }

    private static OperationResponse<BoundaryRebuildResult> CreateResult(
        GeometryBase geometry,
        SurfaceRebuildRouteKind route,
        int pointCount)
    {
        var warnings = new List<ObjectEditWarning>();
        if (!geometry.IsValid)
        {
            warnings.Add(new ObjectEditWarning
            {
                Code = "RECONSTRUCTION_INVALID",
                Message = "Boundary-driven surface reconstruction produced invalid geometry."
            });
        }

        BoundingBox boundingBox = geometry.GetBoundingBox(true);
        return OperationResponse<BoundaryRebuildResult>.Ok(new BoundaryRebuildResult
        {
            Route = route,
            Geometry = geometry,
            Summary = new SurfaceRebuildPreviewSummary
            {
                GeometryKind = geometry.GetType().Name,
                Route = route,
                PointCount = pointCount,
                IsValid = geometry.IsValid,
                BoundingBox = new GeometryBoundingBoxData
                {
                    Min = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Min),
                    Max = LiveGeometryAnalysisHelpers.ToPointData(boundingBox.Max)
                }
            },
            Warnings = warnings
        });
    }

    private static Point3d ToPoint3d(GeometryPointData point)
    {
        return new Point3d(point.X, point.Y, point.Z);
    }
}
