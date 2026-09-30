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

public sealed class LivePanelCladdingMatchService : ILivePanelCladdingMatchService
{
    private readonly ILivePanelCladdingRepository _repository;
    private readonly IPanelCladdingMatchPlanningService _planning;

    public LivePanelCladdingMatchService(
        ILivePanelCladdingRepository repository,
        IPanelCladdingMatchPlanningService planning)
    {
        _repository = repository;
        _planning = planning;
    }

    public OperationResponse<PanelCladdingMatchResult> Match(
        string filePath,
        Guid sourceObjectId,
        IReadOnlyList<Guid> targetObjectIds)
    {
        if (sourceObjectId == Guid.Empty)
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(
                "PANEL_CLADDING_MATCH_SOURCE_REQUIRED");
        }
        Guid[] targetIds = (targetObjectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (targetIds.Length == 0)
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(
                "PANEL_CLADDING_MATCH_TARGET_REQUIRED");
        }
        if (targetIds.Contains(sourceObjectId))
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(
                "PANEL_CLADDING_MATCH_SOURCE_IS_TARGET");
        }

        OperationResponse<PanelCladdingMatchPanelSnapshot> source =
            _repository.ReadMatchPanel(filePath, sourceObjectId);
        if (!source.Success || source.Data is null)
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(
                $"PANEL_CLADDING_MATCH_SOURCE_READ_FAILED: {sourceObjectId:D}: {source.Message}");
        }

        var targets = new List<PanelCladdingMatchPanelSnapshot>(targetIds.Length);
        foreach (Guid targetId in targetIds)
        {
            OperationResponse<PanelCladdingMatchPanelSnapshot> target =
                _repository.ReadMatchPanel(filePath, targetId);
            if (!target.Success || target.Data is null)
            {
                return OperationResponse<PanelCladdingMatchResult>.Fail(
                    $"PANEL_CLADDING_MATCH_TARGET_READ_FAILED: {targetId:D}: {target.Message}");
            }
            targets.Add(target.Data);
        }

        OperationResponse<PanelCladdingMatchPlan> planned = _planning.CreatePlan(source.Data, targets);
        if (!planned.Success || planned.Data is null)
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(planned.Message);
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingMatchResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;

        var prepared = new List<PreparedTarget>(planned.Data.Targets.Count);
        foreach (PanelCladdingMatchTargetPlan targetPlan in planned.Data.Targets)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(targetPlan.ObjectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingMatchResult>.Fail(
                    $"PANEL_CLADDING_MATCH_TARGET_READ_FAILED: {targetPlan.ObjectId:D}: PANEL_CLADDING_BREP_NOT_FOUND");
            }

            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            ApplyPlannedUserText(proposed, targetPlan);
            LivePanelCladdingCidService.Normalize(proposed);
            prepared.Add(new PreparedTarget(rhinoObject, original, proposed, targetPlan));
        }

        uint undoRecord = document.BeginUndoRecord("Match Panel Cladding");
        var modified = new List<PreparedTarget>(prepared.Count);
        try
        {
            foreach (PreparedTarget target in prepared)
            {
                if (!document.Objects.ModifyAttributes(target.Object, target.Proposed, quiet: true))
                {
                    bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                    string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                    return OperationResponse<PanelCladdingMatchResult>.Fail(
                        $"PANEL_CLADDING_MATCH_ATTRIBUTE_COMMIT_FAILED: {target.Object.Id:D}{rollback}");
                }
                modified.Add(target);
                RhinoObject? committed = document.Objects.FindId(target.Object.Id);
                OperationResponse verified = committed is null
                    ? OperationResponse.Fail("PANEL_CLADDING_MATCH_TARGET_MISSING_AFTER_COMMIT")
                    : PanelCladdingMatchPlanningService.ValidateAppliedUserTextPlan(
                        ReadUserText(committed.Attributes),
                        target.Plan);
                if (!verified.Success)
                {
                    bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                    string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                    return OperationResponse<PanelCladdingMatchResult>.Fail(
                        $"PANEL_CLADDING_MATCH_ATTRIBUTE_VERIFY_FAILED: {target.Object.Id:D}: {verified.Message}{rollback}");
                }
            }

            document.Views.Redraw();
            return OperationResponse<PanelCladdingMatchResult>.Ok(new PanelCladdingMatchResult
            {
                SourceObjectId = sourceObjectId,
                UpdatedTargetIds = prepared.Select(target => target.Object.Id).ToArray()
            });
        }
        catch (Exception ex)
        {
            bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
            string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
            return OperationResponse<PanelCladdingMatchResult>.Fail(
                $"PANEL_CLADDING_MATCH_FAILED: {ex.Message}{rollback}");
        }
        finally
        {
            if (undoRecord != 0U)
            {
                document.EndUndoRecord(undoRecord);
            }
        }
    }

    private static void ApplyPlannedUserText(
        ObjectAttributes attributes,
        PanelCladdingMatchTargetPlan plan)
    {
        string?[] existingKeys = attributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
        IReadOnlyDictionary<string, string> current = ReadUserText(attributes);
        IReadOnlyDictionary<string, string> desired =
            PanelCladdingMatchPlanningService.ApplyUserTextPlan(current, plan);
        foreach (string? key in existingKeys)
        {
            if (key is not null && !desired.ContainsKey(key))
            {
                attributes.DeleteUserString(key);
            }
        }
        foreach ((string key, string value) in desired)
        {
            attributes.SetUserString(key, value);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadUserText(ObjectAttributes attributes)
    {
        string?[] keys = attributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = attributes.GetUserString(key) ?? string.Empty;
            }
        }
        return result;
    }

    private static bool RestoreOriginalAttributes(
        RhinoDoc document,
        IReadOnlyList<PreparedTarget> modified)
    {
        bool succeeded = true;
        for (int index = modified.Count - 1; index >= 0; index--)
        {
            PreparedTarget target = modified[index];
            RhinoObject? current = document.Objects.FindId(target.Object.Id);
            succeeded &= current is not null &&
                document.Objects.ModifyAttributes(current, target.Original, quiet: true);
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

    private sealed record PreparedTarget(
        RhinoObject Object,
        ObjectAttributes Original,
        ObjectAttributes Proposed,
        PanelCladdingMatchTargetPlan Plan);
}
