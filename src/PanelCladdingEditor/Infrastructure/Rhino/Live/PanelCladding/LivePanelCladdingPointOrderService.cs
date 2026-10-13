extern alias rhinocommon;

using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using NurbsSurface = rhinocommon::Rhino.Geometry.NurbsSurface;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using Vector3d = rhinocommon::Rhino.Geometry.Vector3d;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using UserData = rhinocommon::Rhino.DocObjects.Custom.UserData;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingPointOrderService
{
    // Prepare only. Each owning command combines geometry with its own Undo/rollback.
    public OperationResponse<PreparedGeometry> Prepare(Brep source, double tolerance)
    {
        if (!source.IsValid || source.Faces.Count != 1 || source.Faces[0].Loops.Count != 1 ||
            !source.Faces[0].IsPlanar(tolerance)) return Skip("requires a planar single-face quad without holes");
        var face = source.Faces[0];
        using var boundary = face.OuterLoop?.To3dCurve();
        if (boundary is null || !boundary.TryGetPolyline(out var polyline)) return Skip("requires a straight four-corner boundary");
        var points = polyline.ToList();
        if (points.Count > 1 && points[0].DistanceTo(points[^1]) <= tolerance) points.RemoveAt(points.Count - 1);
        if (points.Count != 4) return Skip("requires exactly four boundary corners");
        var normal = FrontNormal(source);
        var ordered = new PanelCladdingPointOrderService().Order(points.Select(p => new PanelPoint3(p.X, p.Y, p.Z)).ToArray(),
            new(normal.X, normal.Y, normal.Z), tolerance);
        if (!ordered.Success || ordered.Data is null) return Skip(ordered.Message);
        var p = ordered.Data.Select(point => new Point3d(point.X, point.Y, point.Z)).ToArray();
        using var surface = NurbsSurface.CreateFromCorners(p[0], p[1], p[2], p[3]);
        if (surface is null) return Fail("four-point surface creation failed");
        Brep? replacement = Brep.CreateFromSurface(surface);
        if (replacement is null) return Fail("four-point Brep creation failed");
        // Exact current StandardFourPointSurfaceRebuildSkill sequence: clockwise
        // rebuild, Brep front/back flip, then the default SwapUV operation.
        replacement.Flip();
        replacement.Faces[0].Transpose(true);
        if (!replacement.IsValid || FrontNormal(replacement) * normal < 1 - 1e-8)
        {
            replacement.Dispose();
            return Fail("standard point order did not preserve the panel front normal");
        }
        if (SameParameterization(source, replacement, tolerance))
        {
            replacement.Dispose();
            return OperationResponse<PreparedGeometry>.Ok(new(null, null, ""));
        }
        UserData.Copy(source, replacement);
        UserData.Copy(face, replacement.Faces[0]);
        UserData.Copy(face.UnderlyingSurface(), replacement.Faces[0].UnderlyingSurface());
        foreach (string? key in source.GetUserStrings().AllKeys)
            if (key is not null) replacement.SetUserString(key, source.GetUserString(key));
        return OperationResponse<PreparedGeometry>.Ok(new(source.DuplicateBrep(), replacement, ""));
    }

    private static Vector3d FrontNormal(Brep brep)
    {
        var face = brep.Faces[0];
        var normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
        if (face.OrientationIsReversed) normal.Reverse();
        normal.Unitize();
        return normal;
    }

    private static bool SameParameterization(Brep source, Brep target, double tolerance)
    {
        if (!source.Faces[0].IsSurface || source.Faces[0].OrientationIsReversed != target.Faces[0].OrientationIsReversed) return false;
        using var a = source.Faces[0].ToNurbsSurface();
        using var b = target.Faces[0].ToNurbsSurface();
        if (a is null || b is null || a.Degree(0) != 1 || a.Degree(1) != 1 || a.Points.CountU != 2 || a.Points.CountV != 2) return false;
        for (int u = 0; u < 2; u++)
        for (int v = 0; v < 2; v++)
            if (a.Points.GetControlPoint(u, v).Location.DistanceTo(b.Points.GetControlPoint(u, v).Location) > tolerance ||
                Math.Abs(a.Points.GetControlPoint(u, v).Weight - b.Points.GetControlPoint(u, v).Weight) > 1e-12) return false;
        return true;
    }

    private static OperationResponse<PreparedGeometry> Skip(string reason) => OperationResponse<PreparedGeometry>.Ok(new(null, null, reason));
    private static OperationResponse<PreparedGeometry> Fail(string reason) => OperationResponse<PreparedGeometry>.Fail("PANEL_POINT_ORDER_FAILED: " + reason);

    public sealed class PreparedGeometry(Brep? original, Brep? replacement, string skipReason) : IDisposable
    {
        public Brep? Replacement { get; } = replacement;
        public string SkipReason { get; } = skipReason;
        public bool Changed => Replacement is not null;
        public bool Applied { get; private set; }
        public bool Apply(RhinoDoc document, Guid id)
        {
            if (!Changed) return true;
            Applied = true; // Include attempted writes in rollback even if Rhino returns failure.
            return document.Objects.Replace(id, Replacement!);
        }
        public bool Restore(RhinoDoc document, Guid id) => !Applied || (original is not null && document.Objects.Replace(id, original));
        public void Dispose() { original?.Dispose(); Replacement?.Dispose(); }
    }
}
