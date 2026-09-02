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

public sealed class LivePanelCladdingCurveTemplateService : ILivePanelCladdingCurveTemplateService
{
    private readonly ILivePanelCladdingRepository _repository;
    private readonly PanelCladdingCurveTemplatePlanningService _planning;

    public LivePanelCladdingCurveTemplateService(
        ILivePanelCladdingRepository repository,
        PanelCladdingCurveTemplatePlanningService planning)
    {
        _repository = repository;
        _planning = planning;
    }

    public OperationResponse<PanelCladdingCurveTemplateResult> Apply(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingCurveTemplatePriority priority)
    {
        Guid[] selectedIds = (panelObjectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (selectedIds.Length == 0)
        {
            return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(
                "PANEL_CLADDING_CURVE_TEMPLATE_SELECTION_REQUIRED");
        }

        var snapshots = new List<PanelCladdingCurveTemplatePanelSnapshot>(selectedIds.Length);
        foreach (Guid objectId in selectedIds)
        {
            OperationResponse<PanelCladdingLayout> layout = _repository.ReadLayout(filePath, objectId);
            if (!layout.Success || layout.Data is null)
            {
                return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(
                    $"PANEL_CLADDING_CURVE_TEMPLATE_PANEL_INVALID: {objectId:D}: {layout.Message}");
            }
            snapshots.Add(new PanelCladdingCurveTemplatePanelSnapshot
            {
                ObjectId = objectId,
                HorizontalTrackCount = layout.Data.HorizontalOffsets.Count,
                VerticalTrackCount = layout.Data.VerticalOffsets.Count,
                HasMergeMask = PanelCladdingKeyService.HasNonblankMergeMask(
                    layout.Data.SourceUserText),
                Topology = layout.Data.Topology
            });
        }

        OperationResponse<PanelCladdingCurveTemplatePlan> planned = _planning.CreatePlan(snapshots, priority);
        if (!planned.Success || planned.Data is null)
        {
            return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(planned.Message);
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        var prepared = new List<PreparedPanelMutation>(planned.Data.Panels.Count);
        foreach (PanelCladdingCurveTemplatePanelPlan panelPlan in planned.Data.Panels)
        {
            if (string.IsNullOrWhiteSpace(panelPlan.MergeMask))
            {
                continue;
            }
            RhinoObject? panelObject = document.Objects.FindId(panelPlan.ObjectId);
            if (panelObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(
                    $"PANEL_CLADDING_CURVE_TEMPLATE_BREP_NOT_FOUND: {panelPlan.ObjectId:D}");
            }

            ObjectAttributes original = panelObject.Attributes.Duplicate();
            ObjectAttributes proposed = panelObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.MergeMaskKey);
            proposed.SetUserString(PanelCladdingKeyService.MergeMaskKey, panelPlan.MergeMask);
            if (UserTextEquals(original, proposed))
            {
                continue;
            }
            foreach (string key in panelPlan.UserTextDeletes)
            {
                DeleteUserTextCaseInsensitive(proposed, key);
            }
            prepared.Add(new PreparedPanelMutation(panelObject, original, proposed));
        }

        if (prepared.Count == 0)
        {
            return Success(planned.Data, selectedIds, Array.Empty<Guid>());
        }

        uint undoRecord = document.BeginUndoRecord("Apply Panel Curve Template");
        var modified = new List<PreparedPanelMutation>(prepared.Count);
        try
        {
            foreach (PreparedPanelMutation mutation in prepared)
            {
                if (!document.Objects.ModifyAttributes(mutation.Object, mutation.Proposed, quiet: true))
                {
                    bool rollbackSucceeded = RestoreCommitted(document, modified);
                    string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                    return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(
                        $"PANEL_CLADDING_CURVE_TEMPLATE_ATTRIBUTE_COMMIT_FAILED: " +
                        $"{mutation.Object.Id:D}{rollback}");
                }
                modified.Add(mutation);
            }
            document.Views.Redraw();
            return Success(planned.Data, selectedIds, modified.Select(item => item.Object.Id).ToArray());
        }
        catch (Exception exception)
        {
            bool rollbackSucceeded = RestoreCommitted(document, modified);
            string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
            return OperationResponse<PanelCladdingCurveTemplateResult>.Fail(
                $"PANEL_CLADDING_CURVE_TEMPLATE_FAILED: {exception.Message}{rollback}");
        }
        finally
        {
            if (undoRecord != 0U)
            {
                document.EndUndoRecord(undoRecord);
            }
        }
    }

    private static OperationResponse<PanelCladdingCurveTemplateResult> Success(
        PanelCladdingCurveTemplatePlan plan,
        IReadOnlyList<Guid> selectedIds,
        IReadOnlyList<Guid> updatedIds) =>
        OperationResponse<PanelCladdingCurveTemplateResult>.Ok(new PanelCladdingCurveTemplateResult
        {
            Priority = plan.Priority,
            SelectedPanelIds = selectedIds,
            UpdatedPanelIds = updatedIds,
            MergeRunCount = plan.Panels.Sum(panel => panel.MergeRuns.Count)
        });

    private static void DeleteUserTextCaseInsensitive(ObjectAttributes attributes, string requestedKey)
    {
        string?[] keys = attributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
        foreach (string? key in keys)
        {
            if (key is not null && string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase))
            {
                attributes.DeleteUserString(key);
            }
        }
    }

    private static bool UserTextEquals(ObjectAttributes first, ObjectAttributes second)
    {
        IReadOnlyDictionary<string, string> firstText = ReadUserText(first);
        IReadOnlyDictionary<string, string> secondText = ReadUserText(second);
        return firstText.Count == secondText.Count && firstText.All(pair =>
            secondText.TryGetValue(pair.Key, out string? value) &&
            string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(ObjectAttributes attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = attributes.GetUserStrings();
        if (values?.AllKeys is null)
        {
            return result;
        }
        foreach (string? key in values.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = values[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static bool RestoreCommitted(
        RhinoDoc document,
        IReadOnlyList<PreparedPanelMutation> modified)
    {
        bool succeeded = true;
        for (int index = modified.Count - 1; index >= 0; index--)
        {
            PreparedPanelMutation mutation = modified[index];
            RhinoObject? current = document.Objects.FindId(mutation.Object.Id);
            if (current is null ||
                !document.Objects.ModifyAttributes(current, mutation.Original, quiet: true))
            {
                succeeded = false;
            }
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
        catch (Exception exception)
        {
            return OperationResponse<RhinoDoc>.Fail($"DOCUMENT_PATH_INVALID: {exception.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private sealed record PreparedPanelMutation(
        RhinoObject Object,
        ObjectAttributes Original,
        ObjectAttributes Proposed);
}
