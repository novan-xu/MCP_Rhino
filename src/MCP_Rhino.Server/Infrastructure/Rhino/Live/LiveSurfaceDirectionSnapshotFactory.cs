extern alias rhinocommon;

using MCP_Rhino.Server.Application.Services.Analysis;
using MCP_Rhino.Server.Domain.Models;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BrepFace = rhinocommon::Rhino.Geometry.BrepFace;
using Interval = rhinocommon::Rhino.Geometry.Interval;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Surface = rhinocommon::Rhino.Geometry.Surface;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

internal static class LiveSurfaceDirectionSnapshotFactory
{
    internal static SurfaceDirectionSnapshot Create(Surface surface, string geometryKind)
    {
        Interval uDomain = surface.Domain(0);
        Interval vDomain = surface.Domain(1);
        double u = uDomain.Mid;
        double v = vDomain.Mid;
        Point3d point = surface.PointAt(u, v);
        Vector3d uTangent = Vector3d.Unset;
        Vector3d vTangent = Vector3d.Unset;
        if (surface.Evaluate(u, v, 1, out _, out Vector3d[] derivatives) && derivatives.Length >= 2)
        {
            uTangent = derivatives[0];
            vTangent = derivatives[1];
        }

        Vector3d normal = surface.NormalAt(u, v);
        bool? faceOrientationIsReversed = null;
        if (surface is BrepFace face)
        {
            faceOrientationIsReversed = face.OrientationIsReversed;
            if (face.OrientationIsReversed)
            {
                normal.Reverse();
            }
        }

        return new SurfaceDirectionSnapshot
        {
            GeometryKind = geometryKind,
            UDomain = ToDomainData(uDomain),
            VDomain = ToDomainData(vDomain),
            SamplePoint = LiveGeometryAnalysisHelpers.ToPointData(point),
            UTangent = LiveGeometryAnalysisHelpers.ToVectorData(Unitized(uTangent)),
            VTangent = LiveGeometryAnalysisHelpers.ToVectorData(Unitized(vTangent)),
            Normal = LiveGeometryAnalysisHelpers.ToVectorData(Unitized(normal)),
            FaceOrientationIsReversed = faceOrientationIsReversed
        };
    }

    internal static SurfaceDirectionSnapshot Create(Brep brep, string geometryKind)
    {
        SurfaceDirectionSnapshot snapshot = brep.Faces.Count > 0
            ? Create(brep.Faces[0], geometryKind)
            : new SurfaceDirectionSnapshot { GeometryKind = geometryKind };
        snapshot.SolidOrientation = (int)brep.SolidOrientation;
        return snapshot;
    }

    private static SurfaceDirectionDomainData ToDomainData(Interval interval)
    {
        return new SurfaceDirectionDomainData
        {
            T0 = interval.T0,
            T1 = interval.T1
        };
    }

    private static Vector3d Unitized(Vector3d vector)
    {
        if (!vector.Unitize())
        {
            return new Vector3d();
        }

        return vector;
    }
}
