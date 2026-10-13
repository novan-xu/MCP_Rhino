using PanelCladdingEditor.Domain.Models.PanelCladding;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;

internal static class NativeSmoke
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { Verify(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("Native Rhino acceptance failed", failure);
    }

    private static void Verify()
    {
        using var core = new RhinoCore(["/nosplash"], WindowStyle.NoWindow);
        using var doc = RhinoDoc.CreateHeadless(null);
        doc.ModelAbsoluteTolerance = 0.00001;
        doc.UndoRecordingEnabled = true;
        int parentIndex = doc.Layers.Add(new Layer { Name = "01_CW Panels" });
        int sourceIndex = doc.Layers.Add(new Layer { Name = "Surfaces-PNL", ParentLayerId = doc.Layers[parentIndex].Id });
        int nestedIndex = doc.Layers.Add(new Layer { Name = "WT-01", ParentLayerId = doc.Layers[sourceIndex].Id });
        var ids = new List<Guid>();
        for (int row = 0; row < 2; row++)
        {
            double z = row * 20;
            using var surface = Brep.CreateFromCornerPoints(new(0, 0, z), new(-30, 0, z),
                new(-30, 0, z + 20), new(0, 0, z + 20), doc.ModelAbsoluteTolerance);
            using var attributes = new ObjectAttributes { Name = "preserved-before-undo", LayerIndex = row == 0 ? sourceIndex : nestedIndex };
            attributes.SetUserString("unrelated", "keep");
            attributes.SetUserString("cw_1.05_lot", " Lot 12 ");
            attributes.SetUserString("CW_2.01_UNIT_WIDTH", "stale");
            // Exercise duplicate casing cleanup, even though values are initially blank.
            attributes.SetUserString("cw_1.01_pid", "old");
            attributes.SetUserString("CW_1.06_UNIT_TYPE", row == 0 ? "flat" : "corner_parent");
            ids.Add(doc.Objects.AddBrep(surface, attributes));
        }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        string local = Path.Combine(root!.FullName, "Runtime_Test/local/pcpid");
        Directory.CreateDirectory(local);
        string path = Path.Combine(local, "native-" + Guid.NewGuid().ToString("N") + ".3dm");
        using var options = new FileWriteOptions { UpdateDocumentPath = true, SuppressDialogBoxes = true, SuppressAllInput = true };
        Check(doc.WriteFile(path, options), "Save isolated synthetic fixture");
        Check(!string.IsNullOrWhiteSpace(doc.Path), "Saved headless document path");
        var request = new PanelCladdingPidRequest("BKT", ids, ids[0], ids[0]);
        var service = new LivePanelCladdingPidService();
        var assigned = service.Assign(doc.RuntimeSerialNumber, request);
        Check(assigned.Success && assigned.Data?.UpdatedPanelCount == 2, assigned.Message);
        Check(doc.Objects.FindId(ids[0]).Attributes.GetUserString("CW_1.01_PID") == "PID_BKT_N1_01_01", "Native lower PID");
        Check(doc.Objects.FindId(ids[1]).Attributes.GetUserString("CW_1.02_CID") == "CID_BKT_N1_02_01-P", "Native upper role CID");
        Check(doc.Objects.FindId(ids[0]).Attributes.GetUserString("cw_1.01_pid") is null, "Canonical casing");
        Check(ids.All(id => doc.Objects.FindId(id).Attributes.GetUserString("unrelated") == "keep"), "Unrelated metadata");
        foreach (Guid id in ids)
        {
            var attributes = doc.Objects.FindId(id).Attributes;
            Check(PanelCladdingPidSetupService.RequiredKeys.All(key => attributes.GetUserStrings().AllKeys.Contains(key)), "All 30 setup keys persisted");
            Check(attributes.GetUserString("CW_1.00_PNL") == " " && attributes.GetUserString("CW_1.05_LOT") == " Lot 12 ", "Blank key retained and existing value preserved");
            Check(attributes.GetUserString("CW_2.00_UNIT_DIMENSION") == "30.00000x20.00000" &&
                attributes.GetUserString("CW_2.01_UNIT_WIDTH") == "30.00000" && attributes.GetUserString("CW_2.02_UNIT_HEIGHT") == "20.00000", "Dimensions refreshed from native geometry");
        }
        var repeated = service.Assign(doc.RuntimeSerialNumber, request);
        Check(repeated.Success && repeated.Data?.UpdatedPanelCount == 0, "Repeat makes no writes");
        Check(doc.Undo(), "One Undo");
        Check(ids.All(id => doc.Objects.FindId(id).Attributes.Name == "preserved-before-undo" &&
            doc.Objects.FindId(id).Attributes.GetUserString("CW_1.02_CID") is null), "Undo restores both panels");
        Check(ids.All(id => doc.Objects.FindId(id).Attributes.GetUserString("CW_1.00_PNL") is null &&
            doc.Objects.FindId(id).Attributes.GetUserString("CW_2.01_UNIT_WIDTH") == "stale" &&
            doc.Objects.FindId(id).Attributes.GetUserString("cw_1.05_lot") == " Lot 12 "), "Undo restores missing keys, stale dimensions and original casing");
        uint ambient = doc.BeginUndoRecord("PCpid command simulation");
        Check(ambient != 0, "Ambient undo available");
        var second = service.Assign(doc.RuntimeSerialNumber, request);
        Check(second.Success && doc.CurrentUndoRecordSerialNumber == ambient, "Ambient record reused and left open");
        doc.EndUndoRecord(ambient);
        Check(doc.Undo(), "Ambient undo");
        Check(doc.Objects.Count == 2, "Geometry count unchanged");
        var context = service.GetPanelObjectIds(doc.RuntimeSerialNumber);
        Check(context.Success && context.Data!.ToHashSet().SetEquals(ids), "Root and nested context discovery");
        // The lower panel is context and both references, but only the upper panel is written.
        var partial = service.Assign(doc.RuntimeSerialNumber, request with { PanelObjectIds = [ids[1]] });
        Check(partial.Success && partial.Data?.UpdatedPanelCount == 1 && partial.Data.Plan.ContextPanelCount == 2, partial.Message);
        Check(doc.Objects.FindId(ids[1]).Attributes.GetUserString("CW_1.01_PID") == "PID_BKT_N1_02_01", "Partial native numbering uses unselected row");
        Check(doc.Objects.FindId(ids[0]).Attributes.Name == "preserved-before-undo" &&
            doc.Objects.FindId(ids[0]).Attributes.GetUserString("cw_1.01_pid") == "old" &&
            doc.Objects.FindId(ids[0]).Attributes.GetUserString("CW_1.02_CID") is null, "Unselected panel attributes unchanged");
        Check(doc.Objects.FindId(ids[0]).Attributes.GetUserString("CW_1.00_PNL") is null &&
            doc.Objects.FindId(ids[0]).Attributes.GetUserString("CW_2.01_UNIT_WIDTH") == "stale", "Unselected keys and dimensions unchanged");
        Check(doc.Undo(), "Partial selection undo");
        Check(doc.Objects.Lock(ids[0], true), "Lock context panel");
        context = service.GetPanelObjectIds(doc.RuntimeSerialNumber);
        Check(context.Success && context.Data!.Contains(ids[0]), "Locked unselected panel remains in context");
        partial = service.Assign(doc.RuntimeSerialNumber, request with { PanelObjectIds = [ids[1]] });
        Check(partial.Success && partial.Data?.UpdatedPanelCount == 1, "Locked unselected reference contributes geometry without being written");
        Console.WriteLine("[OK] native geometry extraction, CID roles, canonical writes, idempotence, one Undo and ambient Undo");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
