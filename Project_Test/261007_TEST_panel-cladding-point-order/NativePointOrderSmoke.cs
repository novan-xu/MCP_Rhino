using System.Reflection;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;

internal static class NativePointOrderSmoke
{
    public static void Run()
    {
        Exception? error = null;
        var thread = new Thread(() => { try { Verify(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (error is not null) throw new InvalidOperationException("Native point-order smoke failed", error);
    }

    private static void Verify()
    {
        using var core = new RhinoCore(["/nosplash"], WindowStyle.NoWindow);
        var service = new LivePanelCladdingPointOrderService();
        int count = 0;
        foreach (double angle in new[] { 0d, Math.PI / 2, .71 })
        foreach (bool flip in new[] { false, true })
        foreach (bool transpose in new[] { false, true })
        {
            using var panel = MakePanel();
            panel.Rotate(angle, Vector3d.ZAxis, Point3d.Origin);
            if (flip) panel.Flip();
            if (transpose) panel.Faces[0].Transpose(true);
            panel.SetUserString("geometry-note", "retain");
            panel.UserDictionary.Set("sentinel", "retain-dictionary");
            var normal = Front(panel);
            using var prepared = Required(service.Prepare(panel, 1e-5));
            Check(prepared.SkipReason == "", "Native quad supported");
            var result = prepared.Replacement ?? panel;
            Check(Front(result) * normal > .999999, "Front normal preserved");
            double oldArea = AreaMassProperties.Compute(panel)!.Area;
            double newArea = AreaMassProperties.Compute(result)!.Area;
            Check(Math.Abs(oldArea - newArea) < 1e-5, "Footprint area preserved");
            Check(result.GetUserString("geometry-note") == "retain" && result.UserDictionary.GetString("sentinel") == "retain-dictionary", "Geometry metadata preserved");
            using var again = Required(service.Prepare(result, 1e-5));
            Check(!again.Changed && again.SkipReason == "", "Second standardization is a no-op");
            var face = result.Faces[0];
            var du = face.PointAt(face.Domain(0).T1, face.Domain(1).T0) - face.PointAt(face.Domain(0).T0, face.Domain(1).T0);
            var dv = face.PointAt(face.Domain(0).T0, face.Domain(1).T1) - face.PointAt(face.Domain(0).T0, face.Domain(1).T0);
            Check(du * Vector3d.CrossProduct(Vector3d.ZAxis, normal) > 0 && dv.Z > 0, "Final U runs right, V runs up");
            count++;
        }
        using (var horizontal = Brep.CreateFromCornerPoints(new(0, 0, 0), new(30, 0, 0), new(30, 20, 0), new(0, 20, 0), 1e-5))
        using (var skipped = Required(service.Prepare(horizontal, 1e-5))) Check(!skipped.Changed && skipped.SkipReason.Length > 0, "Horizontal skip is explicit");
        using (var triangle = Brep.CreateFromCornerPoints(new(0, 0, 0), new(-30, 0, 0), new(0, 0, 20), 1e-5))
        using (var skipped = Required(service.Prepare(triangle, 1e-5))) Check(!skipped.Changed && skipped.SkipReason.Length > 0, "Triangle is not filled into a quad");
        Console.WriteLine($"[OK] {count} native standard-skill geometry sequences, normals, UVs, metadata and repeat no-op");

        using var doc = RhinoDoc.CreateHeadless(null);
        doc.ModelAbsoluteTolerance = 1e-5; doc.UndoRecordingEnabled = true;
        int root = doc.Layers.Add("01_CW Panels", System.Drawing.Color.Black);
        int layer = doc.Layers.Add(new Layer { Name = "Surfaces-PNL", ParentLayerId = doc.Layers[root].Id });
        using var source = MakePanel(); source.Faces[0].Transpose(true);
        using var attributes = new ObjectAttributes { LayerIndex = layer, Name = "original-name" };
        attributes.SetUserString("unrelated", "keep");
        Guid id = doc.Objects.AddBrep(source, attributes);
        using var context = source.DuplicateBrep(); context.Translate(new Vector3d(0, 0, 25));
        Guid other = doc.Objects.AddBrep(context, attributes);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        string local = Path.Combine(directory!.FullName, "Runtime_Test/local/panel-point-order");
        Directory.CreateDirectory(local);
        using var options = new FileWriteOptions { UpdateDocumentPath = true, SuppressDialogBoxes = true, SuppressAllInput = true };
        Check(doc.WriteFile(Path.Combine(local, "native-" + Guid.NewGuid().ToString("N") + ".3dm"), options), "Save isolated fixture");
        var pid = new LivePanelCladdingPidService();
        var request = new PanelCladdingPidRequest("BKT", [id], id, id);
        var resultPid = Required(pid.Assign(doc.RuntimeSerialNumber, request));
        Check(resultPid.ReorderedPanelCount == 1 && resultPid.UpdatedPanelCount == 1, "PCpid includes geometry with selected metadata writes");
        Check(doc.Objects.FindId(id).Attributes.GetUserString("unrelated") == "keep" && doc.Objects.FindId(other).Attributes.GetUserString("CW_1.01_PID") is null, "Selected metadata only");
        using (var otherPrepared = Required(service.Prepare((Brep)doc.Objects.FindId(other).Geometry, 1e-5))) Check(otherPrepared.Changed, "Unselected context point order untouched");
        Check(Required(pid.Assign(doc.RuntimeSerialNumber, request)).UpdatedPanelCount == 0, "PCpid repeat changes neither geometry nor metadata");
        Check(doc.Undo(), "One PCpid Undo");
        Check(doc.Objects.FindId(id).Attributes.Name == "original-name" && doc.Objects.FindId(id).Attributes.GetUserString("CW_1.01_PID") is null, "Undo restores metadata");
        using (var restored = Required(service.Prepare((Brep)doc.Objects.FindId(id).Geometry, 1e-5))) Check(restored.Changed, "Undo restores original point order");

        using var updateAttributes = doc.Objects.FindId(id).Attributes.Duplicate();
        updateAttributes.SetUserString("CW_1.01_PID", "PID_BKT_N1_01_01");
        updateAttributes.SetUserString("CW_1.06_UNIT_TYPE", "flat");
        Check(doc.Objects.ModifyAttributes(id, updateAttributes, true), "Seed update identity");
        var observer = new FailingLayout(doc, service);
        var keys = new PanelCladdingKeyService();
        var update = new LivePanelCladdingUpdateService(observer, keys, new PanelCladdingSpawnPlanningService(keys), new PanelCladdingDependencyReconciliationService());
        MethodInfo apply = typeof(LivePanelCladdingUpdateService).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (bool ambient in new[] { false, true })
        {
            uint undo = ambient ? doc.BeginUndoRecord("Point-order test ambient") : 0;
            var failed = (OperationResponse<PanelCladdingUpdateResult>)apply.Invoke(update, [doc, doc.Path, new[] { id }])!;
            Check(!failed.Success && observer.SawStandardizedPanel, "PCUpdate generates from standardized geometry");
            Check(doc.Objects.FindId(id).Attributes.GetUserString("CW_1.02_CID") is null, "Failure restores generated CID");
            using var restored = Required(service.Prepare((Brep)doc.Objects.FindId(id).Geometry, 1e-5));
            Check(restored.Changed, "Failure restores original source point order");
            if (ambient) { Check(doc.CurrentUndoRecordSerialNumber == undo, "Ambient record retained"); doc.EndUndoRecord(undo); }
        }
        Console.WriteLine("[OK] PCpid selection scope, metadata, repeat and single Undo; PCUpdate generation order and owned/ambient failure rollback");
    }

    private static Brep MakePanel() => Brep.CreateFromCornerPoints(new(0, 0, 0), new(-30, 0, 0), new(-30, 0, 20), new(0, 0, 20), 1e-5);
    private static Vector3d Front(Brep b) { var f = b.Faces[0]; var n = f.NormalAt(f.Domain(0).Mid, f.Domain(1).Mid); if (f.OrientationIsReversed) n.Reverse(); n.Unitize(); return n; }
    private static T Required<T>(OperationResponse<T> result) => result.Success && result.Data is not null ? result.Data : throw new InvalidOperationException(result.Message);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class FailingLayout(RhinoDoc doc, LivePanelCladdingPointOrderService ordering) : ILivePanelCladdingRepository
    {
        public bool SawStandardizedPanel { get; private set; }
        public OperationResponse<PanelCladdingLayout> ReadLayout(string path, Guid id)
        {
            using var prepared = Required(ordering.Prepare((Brep)doc.Objects.FindId(id).Geometry, 1e-5));
            SawStandardizedPanel = !prepared.Changed && prepared.SkipReason == "";
            return OperationResponse<PanelCladdingLayout>.Fail("Injected downstream failure");
        }
        public OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string p, Guid id) => throw new NotSupportedException();
        public OperationResponse<PanelAttributeCommitResult> CommitAttributes(PanelAttributeCommitRequest r, Func<OperationResponse> f) => throw new NotSupportedException();
        public OperationResponse<string> SetWorkbookPath(string p, string w) => throw new NotSupportedException();
    }
}
