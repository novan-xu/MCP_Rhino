extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingClearService : ILivePanelCladdingClearService
{
    private readonly PanelCladdingClearPlanningService _planning;

    public LivePanelCladdingClearService(PanelCladdingClearPlanningService planning)
    {
        _planning = planning;
    }

    public OperationResponse<PanelCladdingClearResult> Clear(
        string filePath,
        IReadOnlyList<Guid> objectIds)
    {
        Guid[] selectedIds = (objectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (selectedIds.Length == 0)
        {
            return OperationResponse<PanelCladdingClearResult>.Fail(
                "PANEL_CLADDING_CLEAR_SELECTION_REQUIRED");
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingClearResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;

        var objects = new List<RhinoObject>(selectedIds.Length);
        var snapshots = new List<PanelCladdingClearPanelSnapshot>(selectedIds.Length);
        foreach (Guid objectId in selectedIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingClearResult>.Fail(
                    $"PANEL_CLADDING_CLEAR_BREP_NOT_FOUND: {objectId:D}");
            }

            objects.Add(rhinoObject);
            snapshots.Add(new PanelCladdingClearPanelSnapshot
            {
                ObjectId = objectId,
                UserText = ReadUserText(rhinoObject.Attributes)
            });
        }

        OperationResponse<PanelCladdingClearPlan> planned = _planning.CreatePlan(snapshots);
        if (!planned.Success || planned.Data is null)
        {
            return OperationResponse<PanelCladdingClearResult>.Fail(planned.Message);
        }

        var objectsById = objects.ToDictionary(item => item.Id);
        var prepared = new List<PreparedPanel>();
        foreach (PanelCladdingClearPanelPlan panelPlan in planned.Data.Panels)
        {
            if (panelPlan.UserTextDeletes.Count == 0)
            {
                continue;
            }

            RhinoObject rhinoObject = objectsById[panelPlan.ObjectId];
            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            foreach (string key in panelPlan.UserTextDeletes)
            {
                proposed.DeleteUserString(key);
            }
            prepared.Add(new PreparedPanel(rhinoObject, original, proposed));
        }

        if (prepared.Count == 0)
        {
            return OperationResponse<PanelCladdingClearResult>.Ok(new PanelCladdingClearResult
            {
                SelectedObjectIds = selectedIds,
                UpdatedObjectIds = Array.Empty<Guid>(),
                RemovedKeyCount = 0
            });
        }

        uint undoRecord = document.BeginUndoRecord("Clear Panel Cladding");
        var modified = new List<PreparedPanel>(prepared.Count);
        try
        {
            foreach (PreparedPanel panel in prepared)
            {
                if (!document.Objects.ModifyAttributes(panel.Object, panel.Proposed, quiet: true))
                {
                    bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                    string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                    return OperationResponse<PanelCladdingClearResult>.Fail(
                        $"PANEL_CLADDING_CLEAR_ATTRIBUTE_COMMIT_FAILED: {panel.Object.Id:D}{rollback}");
                }
                modified.Add(panel);
            }

            document.Views.Redraw();
            return OperationResponse<PanelCladdingClearResult>.Ok(new PanelCladdingClearResult
            {
                SelectedObjectIds = selectedIds,
                UpdatedObjectIds = prepared.Select(panel => panel.Object.Id).ToArray(),
                RemovedKeyCount = planned.Data.RemovedKeyCount
            });
        }
        catch (Exception ex)
        {
            bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
            string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
            return OperationResponse<PanelCladdingClearResult>.Fail(
                $"PANEL_CLADDING_CLEAR_FAILED: {ex.Message}{rollback}");
        }
        finally
        {
            if (undoRecord != 0U)
            {
                document.EndUndoRecord(undoRecord);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(ObjectAttributes attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = attributes.GetUserStrings();
        if (values is null)
        {
            return result;
        }
        foreach (string? key in values.AllKeys)
        {
            if (key is not null)
            {
                result[key] = values[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static bool RestoreOriginalAttributes(
        RhinoDoc document,
        IReadOnlyList<PreparedPanel> modified)
    {
        bool succeeded = true;
        for (int index = modified.Count - 1; index >= 0; index--)
        {
            PreparedPanel panel = modified[index];
            RhinoObject? current = document.Objects.FindId(panel.Object.Id);
            succeeded &= current is not null &&
                document.Objects.ModifyAttributes(current, panel.Original, quiet: true);
        }
        return succeeded;
    }

    private static OperationResponse<RhinoDoc> ResolveDocument(string filePath)
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null)
        {
            return OperationResponse<RhinoDoc>.Fail("NO_ACTIVE_DOCUMENT");
        }
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return OperationResponse<RhinoDoc>.Fail("ACTIVE_DOC_UNSAVED");
        }
        try
        {
            if (!string.Equals(
                Path.GetFullPath(document.Path).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<RhinoDoc>.Fail("FILE_NOT_ACTIVE");
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {ex.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private sealed record PreparedPanel(
        RhinoObject Object,
        ObjectAttributes Original,
        ObjectAttributes Proposed);
}
