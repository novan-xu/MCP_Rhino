extern alias rhinocommon;

using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using BoundingBox = rhinocommon::Rhino.Geometry.BoundingBox;
using Plane = rhinocommon::Rhino.Geometry.Plane;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingPidGeometryService
{
    // Measurement only: neither the Brep nor the boundary is transformed/rebuilt.
    public OperationResponse<PanelCladdingPidPanel> Read(Guid id, Brep brep, double tolerance,
        IReadOnlyDictionary<string, string> userText)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0 || !brep.IsValid || brep.Faces.Count != 1 ||
            !brep.Faces[0].TryGetPlane(out var surfacePlane, tolerance))
            return Fail($"Panel {id} must be a planar, single-face surface/Brep.");

        var face = brep.Faces[0];
        var front = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
        if (face.OrientationIsReversed) front.Reverse();
        if (!front.Unitize()) return Fail($"Panel {id} has an invalid face normal.");
        // Use the accepted plane, with the original front's sign. A sampled normal
        // and a bounding-box thickness must not override Rhino's face planarity test.
        var normal = surfacePlane.Normal;
        if (normal * front < 0) normal.Reverse();
        var right = Vector3d.CrossProduct(Vector3d.ZAxis, normal);
        if (!right.Unitize()) return Fail($"Panel {id} is horizontal; select vertical facade panels.");
        var up = Vector3d.CrossProduct(normal, right);
        var frame = new Plane(surfacePlane.Origin, right, up);
        using var boundary = face.OuterLoop?.To3dCurve();
        if (boundary is null || !boundary.IsValid) return Fail($"Panel {id} has an invalid outer boundary.");

        BoundingBox bounds;
        if (boundary.TryGetPolyline(out var polyline))
        {
            var local = new List<Point3d>();
            foreach (var point in polyline)
            {
                if (!frame.ClosestParameter(point, out double x, out double y))
                    return Fail($"Panel {id} has invalid boundary coordinates.");
                local.Add(new Point3d(x, y, 0));
            }
            bounds = new BoundingBox(local);
        }
        else
        {
            // Detached curve bounds respect trims and avoid document-object box
            // caches. Only its in-plane extents are measurements of a planar face.
            bounds = boundary.GetBoundingBox(frame);
        }
        if (!bounds.IsValid) return Fail($"Panel {id} has invalid bounds.");
        Point3d[] corners = [frame.PointAt(bounds.Min.X, bounds.Min.Y), frame.PointAt(bounds.Max.X, bounds.Min.Y),
            frame.PointAt(bounds.Max.X, bounds.Max.Y), frame.PointAt(bounds.Min.X, bounds.Max.Y)];
        return OperationResponse<PanelCladdingPidPanel>.Ok(new(id, new(normal.X, normal.Y, normal.Z),
            corners.Select(p => new PanelPoint3(p.X, p.Y, p.Z)).ToArray(), userText));
    }

    private static OperationResponse<PanelCladdingPidPanel> Fail(string message) =>
        OperationResponse<PanelCladdingPidPanel>.Fail(message);
}
