extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingPidService : ILivePanelCladdingPidService
{
    private readonly PanelCladdingPidPlanningService _planning = new();
    private readonly PanelCladdingPidSetupService _setup = new();

    public OperationResponse<IReadOnlyList<Guid>> GetPanelObjectIds(uint documentSerialNumber)
    {
        if (RhinoApp.InvokeRequired) return OperationResponse<IReadOnlyList<Guid>>.Fail("Run PCpid on Rhino's main thread.");
        var document = RhinoDoc.FromRuntimeSerialNumber(documentSerialNumber);
        if (document is null) return OperationResponse<IReadOnlyList<Guid>>.Fail("The target document is closed.");
        var ids = ReadContextPanels(document).Select(p => p.Id).ToArray();
        return ids.Length == 0
            ? OperationResponse<IReadOnlyList<Guid>>.Fail($"PCpid: no panel surfaces found under {PanelCladdingPidPlanningService.PanelLayerRoot} or its sublayers.")
            : OperationResponse<IReadOnlyList<Guid>>.Ok(ids);
    }

    // Called synchronously from RunCommand on Rhino's UI thread, bound to that
    // command's document rather than whichever document later becomes active.
    public OperationResponse<PanelCladdingPidResult> Assign(uint documentSerialNumber, PanelCladdingPidRequest request)
    {
        if (RhinoApp.InvokeRequired) return Fail("Run PCpid on Rhino's main thread.");
        var document = RhinoDoc.FromRuntimeSerialNumber(documentSerialNumber);
        if (document is null) return Fail("The target document is closed.");
        if (string.IsNullOrWhiteSpace(document.Path)) return Fail("Save the Rhino document before running PCpid.");
        var prepared = new List<AttributeChange>();
        var pointOrder = new List<LivePanelCladdingPointOrderService.PreparedGeometry>();
        var pointOrderWarnings = new List<string>();
        try
        {
            var selectedIds = request.PanelObjectIds.ToHashSet();
            var contextPanels = ReadContextPanels(document);
            var contextIds = contextPanels.Select(p => p.Id).ToHashSet();
            if (selectedIds.Count == 0 || !selectedIds.IsSubsetOf(contextIds))
                return Fail($"Select panel surfaces under {PanelCladdingPidPlanningService.PanelLayerRoot} or its sublayers.");
            var snapshots = new List<PanelCladdingPidPanel>();
            foreach (var panel in contextPanels)
            {
                Guid id = panel.Id;
                if (selectedIds.Contains(id) && (panel.IsDeleted || panel.IsReference || panel.IsLocked || panel.IsHidden))
                    return Fail($"Selected panel {id} is missing, locked, hidden, or a reference object.");
                if (panel.Geometry is not Brep brep)
                    return Fail($"Panel {id} must be a planar, single-face surface/Brep.");
                var measured = new LivePanelCladdingPidGeometryService().Read(id, brep, document.ModelAbsoluteTolerance,
                    LivePanelCladdingCidService.Read(panel.Attributes));
                if (!measured.Success || measured.Data is null) return Fail(measured.Message);
                snapshots.Add(measured.Data);
            }
            var planned = _planning.CreatePlan(request, snapshots, document.ModelAbsoluteTolerance, document.ModelAngleToleranceRadians);
            if (!planned.Success || planned.Data is null) return OperationResponse<PanelCladdingPidResult>.Fail(planned.Message);

            var existing = document.Objects.GetObjectList(new ObjectEnumeratorSettings
            {
                NormalObjects = true, LockedObjects = true, HiddenObjects = true,
                ActiveObjects = true, ReferenceObjects = true
            }).Select(obj => new PanelCladdingPidExistingIdentity(obj.Id,
                ReadIdentity(obj.Attributes, PanelCladdingSpawnPlanningService.PanelIdUserTextKey),
                ReadIdentity(obj.Attributes, PanelCladdingSpawnPlanningService.CidUserTextKey),
                IsManagedLayer(document, obj.Attributes))).ToArray();
            var validated = _planning.ValidateDocumentIdentities(planned.Data, snapshots, existing);
            if (!validated.Success) return OperationResponse<PanelCladdingPidResult>.Fail(validated.Message);

            var snapshotsById = snapshots.ToDictionary(p => p.ObjectId);
            foreach (var assignment in planned.Data.Panels)
            {
                if (!selectedIds.Contains(assignment.ObjectId)) return Fail("A planned write is outside the update selection.");
                var setup = _setup.CreateWrites(assignment, snapshotsById[assignment.ObjectId]);
                if (!setup.Success || setup.Data is null) return OperationResponse<PanelCladdingPidResult>.Fail(setup.Message);
                var panel = document.Objects.FindId(assignment.ObjectId)!;
                var ordered = new LivePanelCladdingPointOrderService().Prepare((Brep)panel.Geometry, document.ModelAbsoluteTolerance);
                if (!ordered.Success || ordered.Data is null) return Fail($"Panel {panel.Id}: {ordered.Message}");
                pointOrder.Add(ordered.Data);
                if (ordered.Data.SkipReason.Length > 0) pointOrderWarnings.Add($"Panel {panel.Id}: {ordered.Data.SkipReason}");
                var original = panel.Attributes.Duplicate();
                var proposed = original.Duplicate();
                var change = new AttributeChange(panel.Id, original, proposed, ordered.Data);
                prepared.Add(change);
                foreach (var write in setup.Data)
                {
                    foreach (string? key in proposed.GetUserStrings().AllKeys)
                    {
                        if (key is not null && string.Equals(key, write.Key, StringComparison.OrdinalIgnoreCase))
                            proposed.DeleteUserString(key);
                    }
                    if (!proposed.SetUserString(write.Key, write.Value))
                        return Fail($"Could not prepare {write.Key} for panel {panel.Id}.");
                }
                proposed.Name = PanelCladdingCidService.ShortName(assignment.Cid);
                var before = LivePanelCladdingCidService.Read(original);
                var after = LivePanelCladdingCidService.Read(proposed);
                change.Changed = ordered.Data.Changed || original.Name != proposed.Name || before.Count != after.Count ||
                    before.Any(p => !after.TryGetValue(p.Key, out string? value) || value != p.Value);
            }
            var changes = prepared.Where(p => p.Changed).ToArray();
            PanelCladdingPidResult Result(int count) => new(planned.Data, count)
            {
                ReorderedPanelCount = pointOrder.Count(p => p.Changed), PointOrderWarnings = pointOrderWarnings.ToArray()
            };
            if (changes.Length == 0) return OperationResponse<PanelCladdingPidResult>.Ok(Result(0));
            uint undoRecord = document.CurrentUndoRecordSerialNumber;
            bool ownsUndoRecord = undoRecord == 0;
            if (ownsUndoRecord) undoRecord = document.BeginUndoRecord("Assign Panel IDs (PCpid)");
            if (undoRecord == 0) return Fail("Rhino Undo is unavailable; no panel IDs were changed.");
            var attempted = new List<AttributeChange>();
            try
            {
                foreach (var change in changes)
                {
                    attempted.Add(change);
                    if (!change.PointOrder.Apply(document, change.ObjectId))
                        throw new InvalidOperationException($"Point-order write failed for panel {change.ObjectId}.");
                    if (!document.Objects.ModifyAttributes(change.ObjectId, change.Proposed, quiet: true))
                        throw new InvalidOperationException($"Attribute write failed for panel {change.ObjectId}.");
                }
            }
            catch (Exception exception)
            {
                bool restored = true;
                foreach (var change in attempted.AsEnumerable().Reverse())
                {
                    try
                    {
                        restored &= change.PointOrder.Restore(document, change.ObjectId);
                        restored &= document.Objects.ModifyAttributes(change.ObjectId, change.Original, quiet: true);
                    }
                    catch { restored = false; }
                }
                return Fail(exception.Message + (restored ? " Original attributes restored." : " ROLLBACK_FAILED: use Rhino Undo to restore the panels."));
            }
            finally
            {
                if (ownsUndoRecord) document.EndUndoRecord(undoRecord);
                document.Views.Redraw();
            }
            return OperationResponse<PanelCladdingPidResult>.Ok(Result(changes.Length));
        }
        catch (Exception exception)
        {
            return Fail(exception.Message);
        }
        finally
        {
            foreach (var change in prepared) { change.Original.Dispose(); change.Proposed.Dispose(); }
            foreach (var geometry in pointOrder) geometry.Dispose();
        }
    }

    private static RhinoObject[] ReadContextPanels(RhinoDoc document) =>
        document.Objects.GetObjectList(new ObjectEnumeratorSettings
        {
            NormalObjects = true, LockedObjects = true, HiddenObjects = true,
            ActiveObjects = true, ReferenceObjects = true,
            ObjectTypeFilter = ObjectType.Surface | ObjectType.Brep
        }).Where(obj => PanelCladdingPidPlanningService.IsPanelLayerPath(LayerPath(document, obj.Attributes))).ToArray();

    private static string LayerPath(RhinoDoc document, ObjectAttributes attributes) =>
        attributes.LayerIndex >= 0 ? document.Layers[attributes.LayerIndex]?.FullPath ?? "" : "";

    private static bool IsManagedLayer(RhinoDoc document, ObjectAttributes attributes)
    {
        string layer = LayerPath(document, attributes);
        return PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(layer) ||
            PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layer);
    }

    private static string ReadIdentity(ObjectAttributes attributes, string key) =>
        (LivePanelCladdingCidService.Read(attributes).FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? "").Trim();

    private static OperationResponse<PanelCladdingPidResult> Fail(string message) =>
        OperationResponse<PanelCladdingPidResult>.Fail("PCpid: " + message);

    private sealed class AttributeChange(Guid objectId, ObjectAttributes original, ObjectAttributes proposed,
        LivePanelCladdingPointOrderService.PreparedGeometry pointOrder)
    {
        public Guid ObjectId { get; } = objectId;
        public ObjectAttributes Original { get; } = original;
        public ObjectAttributes Proposed { get; } = proposed;
        public LivePanelCladdingPointOrderService.PreparedGeometry PointOrder { get; } = pointOrder;
        public bool Changed { get; set; }
    }
}
