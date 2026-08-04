extern alias rhinocommon;

using System.Drawing;
using System.Text;
using Layer = rhinocommon::Rhino.DocObjects.Layer;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using Point3d = rhinocommon::Rhino.Geometry.Point3d;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace MCP_Rhino.Server.Infrastructure.CLI;

// Probe for RhinoCommon layer API behavior. Purpose: validate assumptions made in
// Project_Plan/260421_PLAN_layer-management-tools.md before committing to the design.
// Questions answered:
//   (Q2) LayerTable.Delete(int, quiet=true) on a layer with objects:
//        does it succeed? fail? migrate objects to parent? leave them on IsDeleted=true layer?
//   (Q3) Rhino's UndoManager: does Begin/EndUndoRecord with no document changes
//        leave an empty entry on the Undo stack?
//   (Q4) LayerTable.Purge(int, quiet=true) on a parent layer with child layers:
//        does it cascade-delete children and their objects?
// Entry: Rhino command _McpLayerBehaviorProbe (see McpLayerBehaviorProbeCommand.cs).
// Output: written to stdout (captured by the plugin host and relayed to RhinoApp) and
// appended to _validation/layer-behavior-check/probe-report.txt for later pasting.
public sealed partial class DeveloperCommandHandler
{
    private bool HandleLayerBehaviorProbe(string[] args)
    {
        RhinoDoc? doc = RhinoDoc.ActiveDoc;
        if (doc is null)
        {
            Console.Error.WriteLine("Layer behavior probe requires an active RhinoDoc (run via _McpLayerBehaviorProbe inside Rhino).");
            System.Environment.ExitCode = 1;
            return true;
        }

        var report = new StringBuilder();
        report.AppendLine("# Layer behavior probe report");
        report.AppendLine($"- Timestamp: {DateTime.UtcNow:O}");
        report.AppendLine($"- Doc path: {(string.IsNullOrWhiteSpace(doc.Path) ? "<unsaved>" : doc.Path)}");
        report.AppendLine($"- Rhino version: {RhinoApp.Version}");
        report.AppendLine();

        try
        {
            ProbeDeleteBehavior(doc, report);
            report.AppendLine();
            ProbePurgeCascade(doc, report);
            report.AppendLine();
            ProbeUndoAutoDiscard(doc, report);
        }
        catch (Exception ex)
        {
            report.AppendLine();
            report.AppendLine($"PROBE ABORTED: {ex}");
            System.Environment.ExitCode = 1;
        }

        string output = report.ToString();
        Console.WriteLine(output);
        TryWriteReport(output);
        Console.WriteLine("Layer behavior probe complete. Ctrl+Z in Rhino to unwind probe layers/objects.");
        return true;
    }

    private static void ProbeDeleteBehavior(RhinoDoc doc, StringBuilder report)
    {
        report.AppendLine("## Q2: LayerTable.Delete on a layer with one object (quiet=true)");

        string layerName = $"__McpProbe_Delete_{Guid.NewGuid():N}";
        int layerIndex = doc.Layers.Add(layerName, Color.OrangeRed);
        if (layerIndex < 0)
        {
            report.AppendLine($"- Setup FAILED: Layers.Add returned {layerIndex}.");
            return;
        }

        Guid layerId = doc.Layers[layerIndex].Id;
        Guid pointId = doc.Objects.AddPoint(new Point3d(0, 0, 0), new ObjectAttributes { LayerIndex = layerIndex });
        report.AppendLine($"- Created layer `{layerName}` (Index={layerIndex}, Id={layerId}) with 1 point (Id={pointId}).");

        bool deleteResult = doc.Layers.Delete(layerIndex, quiet: true);
        report.AppendLine($"- doc.Layers.Delete(index, quiet=true) returned: **{deleteResult}**");

        Layer? layerAfter = doc.Layers.FindIndex(layerIndex);
        report.AppendLine($"- doc.Layers.FindIndex(index) after delete: {(layerAfter is null ? "null" : $"layer exists, IsDeleted={layerAfter.IsDeleted}")}");

        RhinoObject? pointAfter = doc.Objects.FindId(pointId);
        if (pointAfter is null)
        {
            report.AppendLine("- Point object: no longer findable via doc.Objects.FindId (possibly removed or hidden).");
        }
        else
        {
            Layer? pointLayer = doc.Layers.FindIndex(pointAfter.Attributes.LayerIndex);
            report.AppendLine($"- Point object: still in doc, LayerIndex={pointAfter.Attributes.LayerIndex}, " +
                              $"layer={(pointLayer is null ? "null" : pointLayer.FullPath)}, " +
                              $"IsDeleted={pointLayer?.IsDeleted}");
        }

        report.AppendLine($"- Interpretation guide: if Delete returned false -> layer refused deletion because of persistent references. " +
                          $"If true and point's LayerIndex unchanged -> soft delete (layer flagged IsDeleted). " +
                          $"If true and point's LayerIndex changed to parent's index -> object migrated.");
    }

    private static void ProbePurgeCascade(RhinoDoc doc, StringBuilder report)
    {
        report.AppendLine("## Q4: LayerTable.Purge on a parent layer with a child layer (each with 1 object)");

        string parentName = $"__McpProbe_Purge_Parent_{Guid.NewGuid():N}";
        int parentIndex = doc.Layers.Add(parentName, Color.MediumPurple);
        if (parentIndex < 0)
        {
            report.AppendLine($"- Setup FAILED: parent Layers.Add returned {parentIndex}.");
            return;
        }

        Guid parentId = doc.Layers[parentIndex].Id;
        var child = new Layer
        {
            Name = "Child",
            ParentLayerId = parentId,
            Color = Color.LightSalmon
        };
        int childIndex = doc.Layers.Add(child);
        if (childIndex < 0)
        {
            report.AppendLine($"- Setup FAILED: child Layers.Add returned {childIndex}.");
            doc.Layers.Delete(parentIndex, quiet: true);
            return;
        }

        Guid parentPointId = doc.Objects.AddPoint(new Point3d(1, 0, 0), new ObjectAttributes { LayerIndex = parentIndex });
        Guid childPointId = doc.Objects.AddPoint(new Point3d(2, 0, 0), new ObjectAttributes { LayerIndex = childIndex });
        report.AppendLine($"- Created parent `{parentName}` (Index={parentIndex}) with point Id={parentPointId}.");
        report.AppendLine($"- Created child `{parentName}::Child` (Index={childIndex}) with point Id={childPointId}.");

        bool purgeResult = doc.Layers.Purge(parentIndex, quiet: true);
        report.AppendLine($"- doc.Layers.Purge(parentIndex, quiet=true) returned: **{purgeResult}**");

        Layer? parentAfter = doc.Layers.FindIndex(parentIndex);
        Layer? childAfter = doc.Layers.FindIndex(childIndex);
        report.AppendLine($"- Parent after: {(parentAfter is null ? "null" : $"IsDeleted={parentAfter.IsDeleted}, Name={parentAfter.Name}")}");
        report.AppendLine($"- Child after: {(childAfter is null ? "null" : $"IsDeleted={childAfter.IsDeleted}, Name={childAfter.Name}, FullPath={childAfter.FullPath}")}");

        RhinoObject? parentPointAfter = doc.Objects.FindId(parentPointId);
        RhinoObject? childPointAfter = doc.Objects.FindId(childPointId);
        report.AppendLine($"- Parent point after: {(parentPointAfter is null ? "GONE" : $"still present, LayerIndex={parentPointAfter.Attributes.LayerIndex}")}");
        report.AppendLine($"- Child point after: {(childPointAfter is null ? "GONE" : $"still present, LayerIndex={childPointAfter.Attributes.LayerIndex}")}");

        report.AppendLine($"- Interpretation guide: `null` / `GONE` on both child & its object -> Purge cascaded. " +
                          $"Only parent removed with child intact (possibly orphaned / IsDeleted=true) -> Purge did NOT cascade.");
    }

    private static void ProbeUndoAutoDiscard(RhinoDoc doc, StringBuilder report)
    {
        report.AppendLine("## Q3: BeginUndoRecord/EndUndoRecord with no document change — does UndoManager keep an empty entry?");

        uint serialBefore = doc.NextUndoRecordSerialNumber;
        uint currentBefore = doc.CurrentUndoRecordSerialNumber;
        report.AppendLine($"- Before probe: NextUndoRecordSerialNumber={serialBefore}, CurrentUndoRecordSerialNumber={currentBefore}");

        uint noOpSerial = doc.BeginUndoRecord("Probe: no-op undo");
        doc.EndUndoRecord(noOpSerial);

        uint serialAfterNoOp = doc.NextUndoRecordSerialNumber;
        uint currentAfterNoOp = doc.CurrentUndoRecordSerialNumber;
        report.AppendLine($"- After no-op Begin/End (serial={noOpSerial}): NextUndoRecordSerialNumber={serialAfterNoOp}, CurrentUndoRecordSerialNumber={currentAfterNoOp}");

        // Do a real mutation inside a record so we can compare.
        uint realSerial = doc.BeginUndoRecord("Probe: real mutation");
        string tempLayerName = $"__McpProbe_UndoMutation_{Guid.NewGuid():N}";
        int tempIndex = doc.Layers.Add(tempLayerName, Color.YellowGreen);
        doc.EndUndoRecord(realSerial);

        uint serialAfterReal = doc.NextUndoRecordSerialNumber;
        uint currentAfterReal = doc.CurrentUndoRecordSerialNumber;
        report.AppendLine($"- After real mutation Begin/Add/End (serial={realSerial}, added layer index={tempIndex}): NextUndoRecordSerialNumber={serialAfterReal}, CurrentUndoRecordSerialNumber={currentAfterReal}");

        report.AppendLine($"- Interpretation guide: compare CurrentUndoRecordSerialNumber around the no-op. " +
                          $"If it advanced after no-op by the same delta as after the real mutation, UndoManager kept an empty entry. " +
                          $"If it only advanced after the real mutation, UndoManager auto-discarded the empty record.");
    }

    private static void TryWriteReport(string output)
    {
        try
        {
            string reportDir = ResolveValidationDirectory("layer-behavior-check");
            string reportPath = Path.Combine(reportDir, $"probe-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(reportPath, output);
            Console.WriteLine($"Probe report written to: {reportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to persist probe report: {ex.Message}");
        }
    }
}
