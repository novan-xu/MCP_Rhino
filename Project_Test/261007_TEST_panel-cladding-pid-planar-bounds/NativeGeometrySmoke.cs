using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Rhino.Geometry;

internal static class NativeGeometrySmoke
{
    // Geometry-only native coverage, without RhinoCore or a document. No file IO.
    public static void Run(PanelPoint3[] points)
    {
        var service = new LivePanelCladdingPidGeometryService();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var request = new PanelCladdingPidRequest("TST", [id], id, id);
        var planner = new PanelCladdingPidPlanningService();
        var metadata = new Dictionary<string, string> { ["unrelated"] = "keep" };
        var p = points.Select(v => new Point3d(v.X, v.Y, v.Z)).ToArray();
        int count = 0;
        foreach (double rotation in new[] { 0d, .71, Math.PI / 2 })
        foreach (bool reverse in new[] { false, true })
        foreach (bool transpose in new[] { false, true })
        {
            using var surface = NurbsSurface.CreateFromCorners(p[0], p[1], p[2], p[3]);
            using var brep = surface.ToBrep();
            brep.Rotate(rotation, Vector3d.ZAxis, Point3d.Origin);
            if (reverse) brep.Flip();
            if (transpose) brep.Faces[0].Transpose(true);
            var beforeNormal = Front(brep);
            var beforeBox = brep.GetBoundingBox(true);
            var result = service.Read(id, brep, 1e-5, metadata);
            Check(result.Success, result.Message);
            var snapshot = result.Data!;
            Check(snapshot.Bounds.Count == 4 && snapshot.UserText["unrelated"] == "keep", "Snapshot contract");
            Check(new Vector3d(snapshot.Normal.X, snapshot.Normal.Y, snapshot.Normal.Z) * beforeNormal > .999999, "Oriented plane preserves front");
            var plan = planner.CreatePlan(request, [snapshot], 1e-5, Math.PI / 1800);
            Check(plan.Success, plan.Message);
            var setup = new PanelCladdingPidSetupService().CreateWrites(plan.Data!.Panels.Single(), snapshot);
            Check(setup.Success && setup.Data!["CW_2.00_UNIT_DIMENSION"] == "90.00000x180.00000", "Accurate measured dimensions");
            Check(brep.GetBoundingBox(true).Equals(beforeBox) && Front(brep) == beforeNormal, "Measurement leaves geometry unchanged");
            count++;
        }
        var plane = new Plane(new Point3d(1234, 5678, 910), Vector3d.YAxis, Vector3d.ZAxis);
        using var rectangle = new Rectangle3d(plane, 30, 60).ToNurbsCurve();
        using var trimmed = Brep.CreatePlanarBreps(rectangle, 1e-5).Single();
        VerifyDimensions(trimmed, "30.00000x60.00000");
        using var circle = new Circle(plane, 10).ToNurbsCurve();
        using var round = Brep.CreatePlanarBreps(circle, 1e-5).Single();
        VerifyDimensions(round, "20.00000x20.00000");
        using var warped = NurbsSurface.CreateFromCorners(p[0], p[1], p[2], p[3] + new Vector3d(.1, 0, 0)).ToBrep();
        Check(!service.Read(id, warped, 1e-5, metadata).Success, "Actual warped face rejected before projection");
        using var horizontal = new PlaneSurface(Plane.WorldXY, new Interval(0, 30), new Interval(0, 60)).ToBrep();
        Check(!service.Read(id, horizontal, 1e-5, metadata).Success, "Horizontal rejected");
        Console.WriteLine($"PASS: native geometry {count} reported-panel variants, trims, curved boundary, nonplanar/horizontal rejection and read-only measurement");

        void VerifyDimensions(Brep brep, string expected)
        {
            var measured = service.Read(id, brep, 1e-5, metadata);
            Check(measured.Success, measured.Message);
            var planned = planner.CreatePlan(request, [measured.Data!], 1e-5, Math.PI / 1800);
            Check(planned.Success, planned.Message);
            var writes = new PanelCladdingPidSetupService().CreateWrites(planned.Data!.Panels.Single(), measured.Data!);
            Check(writes.Success && writes.Data!["CW_2.00_UNIT_DIMENSION"] == expected, "Trim/curve extents");
        }
    }
    private static Vector3d Front(Brep brep)
    {
        var face = brep.Faces[0];
        var normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
        if (face.OrientationIsReversed) normal.Reverse();
        normal.Unitize();
        return normal;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
