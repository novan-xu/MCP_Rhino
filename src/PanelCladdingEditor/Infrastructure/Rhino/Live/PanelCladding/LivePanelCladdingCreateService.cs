extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingCreateService : ILivePanelCladdingCreateService
{
    private readonly PanelCladdingCreatePlanningService _planning;

    public LivePanelCladdingCreateService(PanelCladdingCreatePlanningService planning)
    {
        _planning = planning;
    }

    public OperationResponse<PanelCladdingCreateResult> Create(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        IReadOnlyList<Guid> guideCurveObjectIds)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingCreateResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        Guid[] panelIds = (panelObjectIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        Guid[] guideIds = (guideCurveObjectIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (panelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingCreateResult>.Fail(
                "PANEL_CLADDING_CREATE_PANEL_SELECTION_REQUIRED");
        }
        if (guideIds.Length == 0)
        {
            return OperationResponse<PanelCladdingCreateResult>.Fail(
                "PANEL_CLADDING_CREATE_GUIDE_SELECTION_REQUIRED");
        }

        var guides = new List<LivePanelCladdingGuideCurve>(guideIds.Length);
        foreach (Guid guideId in guideIds)
        {
            RhinoObject? guideObject = document.Objects.FindId(guideId);
            if (guideObject?.Geometry is not Curve curve)
            {
                return OperationResponse<PanelCladdingCreateResult>.Fail(
                    $"PANEL_CLADDING_CREATE_CURVE_NOT_FOUND: {guideId:D}");
            }
            guides.Add(new LivePanelCladdingGuideCurve(guideId, curve));
        }

        var snapshots = new List<PanelCladdingCreatePanelSnapshot>(panelIds.Length);
        foreach (Guid panelId in panelIds)
        {
            RhinoObject? panelObject = document.Objects.FindId(panelId);
            if (panelObject?.Geometry is not Brep brep)
            {
                return OperationResponse<PanelCladdingCreateResult>.Fail(
                    $"PANEL_CLADDING_CREATE_BREP_NOT_FOUND: {panelId:D}");
            }
            OperationResponse<PanelCladdingCreatePanelSnapshot> snapshot =
                LivePanelCladdingGeometryPartitionService.BuildCreateSnapshot(
                    panelId,
                    brep,
                    guides,
                    ReadUserText(panelObject.Attributes),
                    document.ModelAbsoluteTolerance);
            if (!snapshot.Success || snapshot.Data is null)
            {
                return OperationResponse<PanelCladdingCreateResult>.Fail(
                    $"PANEL_CLADDING_CREATE_PANEL_INVALID: {panelId:D}: {snapshot.Message}");
            }
            snapshots.Add(snapshot.Data);
        }

        OperationResponse<PanelCladdingCreatePlan> planned = _planning.CreatePlan(snapshots);
        if (!planned.Success || planned.Data is null)
        {
            return OperationResponse<PanelCladdingCreateResult>.Fail(planned.Message);
        }

        var prepared = new List<PreparedPanelMutation>();
        foreach (PanelCladdingCreatePanelPlan panelPlan in planned.Data.Panels)
        {
            RhinoObject? panelObject = document.Objects.FindId(panelPlan.ObjectId);
            if (panelObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingCreateResult>.Fail(
                    $"PANEL_CLADDING_CREATE_BREP_NOT_FOUND: {panelPlan.ObjectId:D}");
            }
            ObjectAttributes original = panelObject.Attributes.Duplicate();
            ObjectAttributes proposed = panelObject.Attributes.Duplicate();
            foreach (string key in panelPlan.UserTextDeletes)
            {
                proposed.DeleteUserString(key);
            }
            foreach ((string key, string value) in panelPlan.UserTextWrites)
            {
                proposed.SetUserString(key, value);
            }
            bool identityChanged = LivePanelCladdingCidService.Normalize(proposed);
            if (identityChanged || !UserTextEquals(original, proposed))
            {
                prepared.Add(new PreparedPanelMutation(panelObject, original, proposed));
            }
        }

        var modified = new List<PreparedPanelMutation>(prepared.Count);
        if (prepared.Count > 0)
        {
            uint undoRecord = document.BeginUndoRecord("Create Panel Cladding Grid");
            try
            {
                foreach (PreparedPanelMutation mutation in prepared)
                {
                    if (!document.Objects.ModifyAttributes(
                            mutation.Object,
                            mutation.Proposed,
                            quiet: true))
                    {
                        bool rollbackSucceeded = RestoreCommitted(document, modified);
                        string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                        return OperationResponse<PanelCladdingCreateResult>.Fail(
                            $"PANEL_CLADDING_CREATE_ATTRIBUTE_COMMIT_FAILED: {mutation.Object.Id:D}{rollback}");
                    }
                    modified.Add(mutation);
                }
            }
            catch (Exception exception)
            {
                bool rollbackSucceeded = RestoreCommitted(document, modified);
                string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                return OperationResponse<PanelCladdingCreateResult>.Fail(
                    $"PANEL_CLADDING_CREATE_FAILED: {exception.Message}{rollback}");
            }
            finally
            {
                if (undoRecord != 0U)
                {
                    document.EndUndoRecord(undoRecord);
                }
            }
            document.Views.Redraw();
        }

        return OperationResponse<PanelCladdingCreateResult>.Ok(new PanelCladdingCreateResult
        {
            SelectedPanelIds = planned.Data.Panels.Select(panel => panel.ObjectId).ToArray(),
            UpdatedPanelIds = modified.Select(mutation => mutation.Object.Id).ToArray(),
            HorizontalOffsetCount = planned.Data.Panels.Sum(panel => panel.HorizontalOffsets.Count),
            VerticalOffsetCount = planned.Data.Panels.Sum(panel => panel.VerticalOffsets.Count),
            CellCount = planned.Data.Panels.Sum(panel => panel.CellCount),
            Warnings = planned.Data.Panels.SelectMany(panel => panel.Warnings).ToArray()
        });
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
            return OperationResponse<RhinoDoc>.Fail(
                $"DOCUMENT_PATH_INVALID: {exception.Message}");
        }
        return OperationResponse<RhinoDoc>.Ok(document);
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(ObjectAttributes attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var strings = attributes.GetUserStrings();
        if (strings?.AllKeys is null)
        {
            return result;
        }
        foreach (string? key in strings.AllKeys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = strings[key] ?? string.Empty;
            }
        }
        return result;
    }

    private static bool UserTextEquals(ObjectAttributes first, ObjectAttributes second)
    {
        IReadOnlyDictionary<string, string> firstText = ReadUserText(first);
        IReadOnlyDictionary<string, string> secondText = ReadUserText(second);
        return firstText.Count == secondText.Count && firstText.All(pair =>
            secondText.TryGetValue(pair.Key, out string? value) &&
            string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private static bool RestoreCommitted(
        RhinoDoc document,
        IReadOnlyList<PreparedPanelMutation> modified)
    {
        bool succeeded = true;
        foreach (PreparedPanelMutation mutation in modified.Reverse())
        {
            RhinoObject? current = document.Objects.FindId(mutation.Object.Id);
            if (current is null ||
                !document.Objects.ModifyAttributes(current, mutation.Original, quiet: true))
            {
                succeeded = false;
            }
        }
        return succeeded;
    }

    private sealed record PreparedPanelMutation(
        RhinoObject Object,
        ObjectAttributes Original,
        ObjectAttributes Proposed);
}
