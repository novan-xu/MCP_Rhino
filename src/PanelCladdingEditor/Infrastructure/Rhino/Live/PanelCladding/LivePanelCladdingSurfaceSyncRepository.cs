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

public sealed class LivePanelCladdingSurfaceSyncRepository : ILivePanelCladdingSurfaceSyncRepository
{
    private const string CladdingUserTextKey = "Cladding";
    private readonly ILivePanelCladdingRepository _layouts;

    public LivePanelCladdingSurfaceSyncRepository(ILivePanelCladdingRepository layouts)
    {
        _layouts = layouts;
    }

    public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds)
    {
        Guid[] selectedIds = (panelObjectIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (selectedIds.Length == 0)
        {
            return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Fail(
                "PANEL_CLADDING_SURFACE_SYNC_SELECTION_REQUIRED");
        }

        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;

        var panels = new List<PanelCladdingSurfaceSyncPanelSnapshot>(selectedIds.Length);
        var expectedCids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Guid objectId in selectedIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_PANEL_BREP_NOT_FOUND: {objectId:D}");
            }

            OperationResponse<PanelCladdingLayout> layout = _layouts.ReadLayout(filePath, objectId);
            if (!layout.Success || layout.Data is null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_PANEL_READ_FAILED: {objectId:D}: {layout.Message}");
            }
            string pid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
            {
                ObjectId = objectId,
                PanelId = pid,
                Layout = layout.Data
            });
            if (!string.IsNullOrWhiteSpace(pid))
            {
                foreach (PanelCladdingCell cell in layout.Data.Cells)
                {
                    expectedCids.Add(PanelCladdingSpawnPlanningService.BuildSurfaceCid(
                        pid,
                        cell.ShortLabel));
                }
            }
        }

        var surfaces = new List<PanelCladdingSurfaceSyncSurfaceSnapshot>();
        foreach (RhinoObject rhinoObject in document.Objects)
        {
            if (rhinoObject.Geometry is not Brep)
            {
                continue;
            }
            string cid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            if (string.IsNullOrWhiteSpace(cid))
            {
                continue;
            }
            string layerPath = GetLayerPath(document, rhinoObject);
            if (!IsUnderMaterialSurfaceRoot(layerPath) && !expectedCids.Contains(cid.Trim()))
            {
                continue;
            }

            surfaces.Add(new PanelCladdingSurfaceSyncSurfaceSnapshot
            {
                ObjectId = rhinoObject.Id,
                PanelId = GetCanonicalUserText(
                    rhinoObject.Attributes,
                    PanelCladdingSpawnPlanningService.PanelIdUserTextKey),
                Cid = cid,
                LayerPath = layerPath,
                CladdingValue = GetCanonicalUserText(rhinoObject.Attributes, CladdingUserTextKey)
            });
        }

        return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(
            new PanelCladdingSurfaceSyncSnapshot
            {
                DocumentPath = document.Path,
                WorkbookPath = document.Strings.GetValue(
                    PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty,
                Panels = panels,
                Surfaces = surfaces
            });
    }

    public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
        PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook,
        IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(request.FilePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;

        PanelCladdingSurfaceSyncSurfacePlan[] surfaceWrites = (request.SurfaceWrites ??
                Array.Empty<PanelCladdingSurfaceSyncSurfacePlan>())
            .GroupBy(item => item.ObjectId)
            .Select(group => group.First())
            .ToArray();
        PanelCladdingSurfaceSyncPanelWrite[] panelWrites = (request.PanelWrites ??
                Array.Empty<PanelCladdingSurfaceSyncPanelWrite>())
            .GroupBy(item => item.ObjectId)
            .Select(group => group.First())
            .ToArray();
        var prepared = new List<PreparedObject>(surfaceWrites.Length + panelWrites.Length);

        foreach (PanelCladdingSurfaceSyncSurfacePlan surfaceWrite in surfaceWrites)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(surfaceWrite.ObjectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.Cid}: surface not found");
            }
            string currentPid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            string currentCid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            string currentLayerPath = GetLayerPath(document, rhinoObject);
            if (!string.Equals(currentPid.Trim(), surfaceWrite.PanelId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentCid.Trim(), surfaceWrite.Cid, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentLayerPath, surfaceWrite.ExpectedLayerPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.Cid}: PID, CID, or layer changed");
            }

            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, CladdingUserTextKey);
            proposed.SetUserString(CladdingUserTextKey, surfaceWrite.MaterialCode);
            prepared.Add(new PreparedObject(rhinoObject, original, proposed));
        }

        foreach (PanelCladdingSurfaceSyncPanelWrite panelWrite in panelWrites)
        {
            OperationResponse<PanelCladdingLayout> currentLayout =
                _layouts.ReadLayout(request.FilePath, panelWrite.ObjectId);
            if (!currentLayout.Success || currentLayout.Data is null ||
                !string.Equals(
                    currentLayout.Data.GeometryFingerprint,
                    panelWrite.ExpectedGeometryFingerprint,
                    StringComparison.Ordinal))
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_PANEL: {panelWrite.ObjectId:D}");
            }

            RhinoObject? rhinoObject = document.Objects.FindId(panelWrite.ObjectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_PANEL: {panelWrite.ObjectId:D}");
            }
            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacyTypeCodeKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacySignatureKey);
            foreach ((string key, string value) in panelWrite.CellValues)
            {
                proposed.SetUserString(key, value);
            }
            proposed.SetUserString(PanelCladdingKeyService.TypeCodeKey, panelWrite.TypeCode);
            proposed.SetUserString(PanelCladdingKeyService.SignatureKey, panelWrite.StoredSignature);
            prepared.Add(new PreparedObject(rhinoObject, original, proposed));
        }

        if (prepared.Count == 0)
        {
            OperationResponse externalOnly = finalizeWorkbook();
            return externalOnly.Success
                ? OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(BuildResult(
                    request,
                    surfaceWrites,
                    panelWrites,
                    types,
                    matchedSurfaceCount))
                : OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(externalOnly.Message);
        }

        string? oldWorkbookPath = document.Strings.GetValue(
            PanelCladdingKeyService.WorkbookPathDocumentKey);
        bool workbookPathChanged = panelWrites.Length > 0 &&
            !string.IsNullOrWhiteSpace(request.WorkbookPath) &&
            !string.Equals(oldWorkbookPath, request.WorkbookPath, StringComparison.OrdinalIgnoreCase);
        uint undoRecord = document.BeginUndoRecord("Sync Panel Cladding From Surfaces");
        var modified = new List<PreparedObject>(prepared.Count);
        try
        {
            foreach (PreparedObject item in prepared)
            {
                if (!document.Objects.ModifyAttributes(item.Object, item.Proposed, quiet: true))
                {
                    bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                    RestoreDocumentString(document, oldWorkbookPath);
                    string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                    return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                        $"PANEL_CLADDING_SURFACE_SYNC_ATTRIBUTE_COMMIT_FAILED: {item.Object.Id:D}{rollback}");
                }
                modified.Add(item);
            }

            if (workbookPathChanged)
            {
                document.Strings.SetString(
                    PanelCladdingKeyService.WorkbookPathDocumentKey,
                    request.WorkbookPath);
            }

            OperationResponse external = finalizeWorkbook();
            if (!external.Success)
            {
                bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                RestoreDocumentString(document, oldWorkbookPath);
                string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"{external.Message}{rollback}");
            }

            document.Views.Redraw();
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(BuildResult(
                request,
                surfaceWrites,
                panelWrites,
                types,
                matchedSurfaceCount));
        }
        catch (Exception ex)
        {
            bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
            RestoreDocumentString(document, oldWorkbookPath);
            string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_FAILED: {ex.Message}{rollback}");
        }
        finally
        {
            if (undoRecord != 0U)
            {
                document.EndUndoRecord(undoRecord);
            }
        }
    }

    private static PanelCladdingSurfaceSyncResult BuildResult(
        PanelCladdingSurfaceSyncCommitRequest request,
        IReadOnlyList<PanelCladdingSurfaceSyncSurfacePlan> surfaceWrites,
        IReadOnlyList<PanelCladdingSurfaceSyncPanelWrite> panelWrites,
        IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount)
    {
        return new PanelCladdingSurfaceSyncResult
        {
            SelectedPanelIds = request.SelectedPanelIds,
            ChangedPanelIds = panelWrites.Select(item => item.ObjectId).ToArray(),
            RefreshedSurfaceIds = surfaceWrites.Select(item => item.ObjectId).ToArray(),
            MatchedSurfaceCount = matchedSurfaceCount,
            WorkbookPath = panelWrites.Count > 0 ? request.WorkbookPath : string.Empty,
            Types = types
        };
    }

    private static string GetCanonicalUserText(ObjectAttributes attributes, string canonicalKey)
    {
        var values = attributes.GetUserStrings();
        if (values is null)
        {
            return string.Empty;
        }
        foreach (string? key in values.AllKeys)
        {
            if (key is not null && string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                return values[key] ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static void DeleteUserTextCaseInsensitive(ObjectAttributes attributes, string canonicalKey)
    {
        string?[] keys = attributes.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
        foreach (string? key in keys)
        {
            if (key is not null && string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                attributes.DeleteUserString(key);
            }
        }
    }

    private static string GetLayerPath(RhinoDoc document, RhinoObject rhinoObject)
    {
        return rhinoObject.Attributes.LayerIndex >= 0
            ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
            : string.Empty;
    }

    private static bool IsUnderMaterialSurfaceRoot(string layerPath)
    {
        string root = PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer;
        return string.Equals(layerPath, root, StringComparison.OrdinalIgnoreCase) ||
            layerPath.StartsWith(root + "::", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RestoreOriginalAttributes(
        RhinoDoc document,
        IReadOnlyList<PreparedObject> modified)
    {
        bool succeeded = true;
        for (int index = modified.Count - 1; index >= 0; index--)
        {
            PreparedObject item = modified[index];
            RhinoObject? current = document.Objects.FindId(item.Object.Id);
            succeeded &= current is not null &&
                document.Objects.ModifyAttributes(current, item.Original, quiet: true);
        }
        return succeeded;
    }

    private static void RestoreDocumentString(RhinoDoc document, string? oldValue)
    {
        if (string.IsNullOrEmpty(oldValue))
        {
            document.Strings.Delete(PanelCladdingKeyService.WorkbookPathDocumentKey);
        }
        else
        {
            document.Strings.SetString(PanelCladdingKeyService.WorkbookPathDocumentKey, oldValue);
        }
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

    private sealed record PreparedObject(
        RhinoObject Object,
        ObjectAttributes Original,
        ObjectAttributes Proposed);
}
