extern alias rhinocommon;

using System.Globalization;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingSurfaceSyncRepository : ILivePanelCladdingSurfaceSyncRepository
{
    private const string CladdingUserTextKey = "Cladding";
    private readonly ILivePanelCladdingRepository _layouts;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelAxonometricProjectionService _projection;

    public LivePanelCladdingSurfaceSyncRepository(
        ILivePanelCladdingRepository layouts,
        PanelCladdingKeyService keys,
        PanelAxonometricProjectionService projection)
    {
        _layouts = layouts;
        _keys = keys;
        _projection = projection;
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

        var issues = new List<PanelCladdingSurfaceSyncIssue>();
        var candidates = new List<PanelReadCandidate>(selectedIds.Length);
        foreach (Guid objectId in selectedIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                issues.Add(new PanelCladdingSurfaceSyncIssue
                {
                    PanelObjectId = objectId,
                    Message = $"PANEL_CLADDING_SURFACE_SYNC_PANEL_BREP_NOT_FOUND: {objectId:D}"
                });
                continue;
            }

            OperationResponse<PanelCladdingLayout> layout = _layouts.ReadLayout(filePath, objectId);
            if (!layout.Success || layout.Data is null)
            {
                issues.Add(new PanelCladdingSurfaceSyncIssue
                {
                    PanelObjectId = objectId,
                    PanelId = GetCanonicalUserText(
                        rhinoObject.Attributes,
                        PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim(),
                    Message = $"PANEL_CLADDING_SURFACE_SYNC_PANEL_READ_FAILED: {objectId:D}: {layout.Message}"
                });
                continue;
            }
            string pid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            candidates.Add(new PanelReadCandidate(
                rhinoObject,
                (Brep)rhinoObject.Geometry,
                pid,
                layout.Data,
                ReadUserText(rhinoObject)));
        }

        var uniquePanelsByPid = candidates
            .Where(panel => !string.IsNullOrWhiteSpace(panel.PanelId))
            .GroupBy(panel => panel.PanelId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var discoveredSurfaces = new List<SurfaceReadCandidate>();
        var modelTypeAssignments = new List<PanelCladdingWorkbookTypeReference>();
        foreach (RhinoObject rhinoObject in document.Objects.GetObjectList(
            CreateSurfaceEnumeratorSettings()))
        {
            if (rhinoObject.Geometry is not Brep)
            {
                continue;
            }
            string typeCode = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingKeyService.TypeCodeKey).Trim();
            string storedSignature = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingKeyService.SignatureKey).Trim();
            if (typeCode.Length > 0 || storedSignature.Length > 0)
            {
                modelTypeAssignments.Add(new PanelCladdingWorkbookTypeReference
                {
                    ObjectId = rhinoObject.Id,
                    TypeCode = typeCode,
                    StoredSignature = storedSignature
                });
            }
            if (selectedIds.Contains(rhinoObject.Id))
            {
                continue;
            }
            string layerPath = GetLayerPath(document, rhinoObject);
            if (!IsUnderMaterialSurfaceRoot(layerPath))
            {
                continue;
            }
            string cid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            string surfacePid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            if (string.IsNullOrWhiteSpace(surfacePid))
            {
                continue;
            }
            discoveredSurfaces.Add(new SurfaceReadCandidate(
                rhinoObject,
                (Brep)rhinoObject.Geometry,
                surfacePid,
                cid,
                layerPath,
                GetCanonicalUserText(rhinoObject.Attributes, CladdingUserTextKey)));
        }

        var panels = new List<PanelCladdingSurfaceSyncPanelSnapshot>(candidates.Count);
        var surfaces = new List<PanelCladdingSurfaceSyncSurfaceSnapshot>();
        foreach (PanelReadCandidate panel in candidates)
        {
            string pid = panel.PanelId.Trim();
            if (pid.Length == 0 || !uniquePanelsByPid.TryGetValue(pid, out PanelReadCandidate? uniquePanel) ||
                uniquePanel.Object.Id != panel.Object.Id)
            {
                panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = panel.Object.Id,
                    PanelId = panel.PanelId,
                    Layout = panel.Layout
                });
                continue;
            }

            SurfaceReadCandidate[] panelSurfaces = discoveredSurfaces
                .Where(surface => string.Equals(
                    surface.PanelId.Trim(),
                    pid,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (panelSurfaces.Length == 0)
            {
                issues.Add(new PanelCladdingSurfaceSyncIssue
                {
                    PanelObjectId = panel.Object.Id,
                    PanelId = pid,
                    Message = $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {pid}: no Breps found under {PanelCladdingSpawnPlanningService.MaterialSurfaceRootLayer}."
                });
                panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = panel.Object.Id,
                    PanelId = panel.PanelId,
                    Layout = panel.Layout
                });
                continue;
            }

            OperationResponse<PanelCladdingInferredOffsets> inferred =
                LivePanelCladdingGeometryPartitionService.InferOffsets(
                    panel.Geometry,
                    panelSurfaces.Select(surface => surface.Geometry).ToArray(),
                    panel.Layout.ModelTolerance);
            if (!inferred.Success || inferred.Data is null)
            {
                AddPanelIssue(panel, $"PANEL_CLADDING_SURFACE_SYNC_OFFSET_INFERENCE_FAILED: {inferred.Message}");
                continue;
            }
            OperationResponse<PanelCladdingKeySet> keySet = _keys.CreateKeySet(
                inferred.Data.HorizontalOffsets,
                inferred.Data.VerticalOffsets,
                panel.UserText,
                panel.Layout.Width,
                panel.Layout.Height,
                panel.Layout.ModelTolerance);
            if (!keySet.Success || keySet.Data is null)
            {
                AddPanelIssue(panel, $"PANEL_CLADDING_SURFACE_SYNC_INFERRED_GRID_INVALID: {keySet.Message}");
                continue;
            }
            OperationResponse<PanelCladdingLayout> inferredLayout = BuildInferredLayout(
                panel.Layout,
                keySet.Data);
            if (!inferredLayout.Success || inferredLayout.Data is null)
            {
                AddPanelIssue(panel, inferredLayout.Message);
                continue;
            }
            OperationResponse<LivePanelCladdingGeometryGrid> geometryGrid =
                LivePanelCladdingGeometryPartitionService.CreateGrid(
                    panel.Geometry,
                    keySet.Data,
                    panel.Layout.ModelTolerance);
            if (!geometryGrid.Success || geometryGrid.Data is null)
            {
                AddPanelIssue(panel,
                    $"PANEL_CLADDING_SURFACE_SYNC_GRID_FAILED: {panel.Object.Id:D}: {geometryGrid.Message}");
                continue;
            }

            using LivePanelCladdingGeometryGrid grid = geometryGrid.Data;
            var expectedCids = keySet.Data.Cells
                .Select(cell => PanelCladdingSpawnPlanningService.BuildSurfaceCid(pid, cell.ShortLabel))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            SurfaceReadCandidate? wrongPid = discoveredSurfaces.FirstOrDefault(surface =>
                !string.IsNullOrWhiteSpace(surface.Cid) &&
                expectedCids.Contains(surface.Cid.Trim()) &&
                !string.Equals(surface.PanelId.Trim(), pid, StringComparison.OrdinalIgnoreCase));
            if (wrongPid is not null)
            {
                AddPanelIssue(panel,
                    $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_PID_MISMATCH: {wrongPid.Cid.Trim()}: expected {pid}, found {wrongPid.PanelId.Trim()}");
                continue;
            }

            foreach (SurfaceReadCandidate surface in panelSurfaces)
            {
                OperationResponse<IReadOnlyList<string>> coverage =
                    LivePanelCladdingGeometryPartitionService.ResolveCoveredCellLabels(
                        surface.Geometry,
                        grid,
                        panel.Layout.ModelTolerance,
                        string.IsNullOrWhiteSpace(surface.Cid)
                            ? surface.Object.Id.ToString("D")
                            : surface.Cid.Trim());
                IReadOnlyList<string> coveredCellLabels = Array.Empty<string>();
                if (!coverage.Success || coverage.Data is null)
                {
                    issues.Add(new PanelCladdingSurfaceSyncIssue
                    {
                        PanelObjectId = panel.Object.Id,
                        PanelId = pid,
                        Message = coverage.Message
                    });
                }
                else
                {
                    coveredCellLabels = coverage.Data;
                }
                surfaces.Add(new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = surface.Object.Id,
                    PanelId = surface.PanelId,
                    Cid = surface.Cid,
                    LayerPath = surface.LayerPath,
                    CladdingValue = surface.CladdingValue,
                    CoveredCellLabels = coveredCellLabels
                });
            }

            panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
            {
                ObjectId = panel.Object.Id,
                PanelId = panel.PanelId,
                Layout = inferredLayout.Data,
                GridChanged = !OffsetsEqual(
                    panel.Layout.HorizontalOffsets,
                    inferredLayout.Data.HorizontalOffsets,
                    panel.Layout.ModelTolerance) ||
                    !OffsetsEqual(
                        panel.Layout.VerticalOffsets,
                        inferredLayout.Data.VerticalOffsets,
                        panel.Layout.ModelTolerance) ||
                    !_keys.AreOffsetsCanonicallyStored(
                        panel.UserText,
                        inferredLayout.Data.HorizontalOffsets,
                        inferredLayout.Data.VerticalOffsets)
            });
        }
        return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(
            new PanelCladdingSurfaceSyncSnapshot
            {
                DocumentPath = document.Path,
                WorkbookPath = document.Strings.GetValue(
                    PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty,
                SelectedPanelIds = selectedIds,
                Panels = panels,
                Surfaces = surfaces,
                Issues = issues,
                ModelTypeAssignments = modelTypeAssignments
            });

        void AddPanelIssue(PanelReadCandidate panel, string message)
        {
            issues.Add(new PanelCladdingSurfaceSyncIssue
            {
                PanelObjectId = panel.Object.Id,
                PanelId = panel.PanelId.Trim(),
                Message = message
            });
            panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
            {
                ObjectId = panel.Object.Id,
                PanelId = panel.PanelId,
                Layout = panel.Layout
            });
        }
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
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.ExpectedCid}: surface not found");
            }
            string currentPid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            string currentCid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey);
            string currentLayerPath = GetLayerPath(document, rhinoObject);
            if (!string.Equals(currentPid.Trim(), surfaceWrite.PanelId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(currentCid.Trim(), surfaceWrite.ExpectedCid, StringComparison.Ordinal) ||
                !string.Equals(currentLayerPath, surfaceWrite.ExpectedLayerPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.ExpectedCid}: PID, CID, or layer changed");
            }

            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, CladdingUserTextKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingSpawnPlanningService.CidUserTextKey);
            proposed.SetUserString(CladdingUserTextKey, surfaceWrite.MaterialCode);
            proposed.SetUserString(
                PanelCladdingSpawnPlanningService.CidUserTextKey,
                surfaceWrite.DesiredCid);
            if (string.IsNullOrWhiteSpace(proposed.Name) ||
                string.Equals(proposed.Name, surfaceWrite.ExpectedCid, StringComparison.OrdinalIgnoreCase))
            {
                proposed.Name = surfaceWrite.DesiredCid;
            }
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
            string?[] existingKeys = proposed.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
            foreach (string? key in existingKeys)
            {
                if (key is not null && (_keys.IsOffsetKey(key) || _keys.IsCladdingCellKey(key)))
                {
                    proposed.DeleteUserString(key);
                }
            }
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacyTypeCodeKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacySignatureKey);
            for (int index = 0; index < panelWrite.HorizontalOffsets.Count; index++)
            {
                proposed.SetUserString(
                    PanelCladdingKeyService.GetHorizontalOffsetKey(index),
                    PanelCladdingKeyService.FormatOffset(panelWrite.HorizontalOffsets[index]));
            }
            for (int index = 0; index < panelWrite.VerticalOffsets.Count; index++)
            {
                proposed.SetUserString(
                    PanelCladdingKeyService.GetVerticalOffsetKey(index),
                    PanelCladdingKeyService.FormatOffset(panelWrite.VerticalOffsets[index]));
            }
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
            if (!externalOnly.Success)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(externalOnly.Message);
            }
            SelectSkippedPanels(document, request.SkippedPanelIds);
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Ok(BuildResult(
                request,
                surfaceWrites,
                panelWrites,
                types,
                matchedSurfaceCount));
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

            SelectSkippedPanels(document, request.SkippedPanelIds);
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
            SkippedPanelIds = request.SkippedPanelIds,
            ChangedPanelIds = panelWrites.Select(item => item.ObjectId).ToArray(),
            RefreshedSurfaceIds = surfaceWrites.Select(item => item.ObjectId).ToArray(),
            MatchedSurfaceCount = matchedSurfaceCount,
            WorkbookPath = request.WorkbookPath,
            Types = types,
            Issues = request.Issues,
            RemovedWorkbookTypeCodes = request.RemovedWorkbookTypeCodes
        };
    }

    private OperationResponse<PanelCladdingLayout> BuildInferredLayout(
        PanelCladdingLayout source,
        PanelCladdingKeySet keySet)
    {
        if (source.Preview.Vertices.Count < 3 || source.Preview.Triangles.Count == 0)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_PREVIEW_GEOMETRY_MISSING: {source.ObjectId:D}");
        }
        double xMin = source.Preview.Vertices.Min(point => point.X);
        double xMax = source.Preview.Vertices.Max(point => point.X);
        double yMin = source.Preview.Vertices.Min(point => point.Y);
        double yMax = source.Preview.Vertices.Max(point => point.Y);
        OperationResponse<(PanelGeometryClass Classification, string Diagnostic, PanelPreviewGeometry Preview)> projection =
            _projection.Build(
                source.Preview.Vertices,
                source.Preview.Triangles,
                xMin,
                xMax,
                yMin,
                yMax,
                keySet.HorizontalOffsets,
                keySet.VerticalOffsets,
                keySet.Cells,
                source.ModelTolerance);
        if (!projection.Success)
        {
            return OperationResponse<PanelCladdingLayout>.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_PREVIEW_FAILED: {source.ObjectId:D}: {projection.Message}");
        }
        var projected = projection.Data;
        return OperationResponse<PanelCladdingLayout>.Ok(new PanelCladdingLayout
        {
            ObjectId = source.ObjectId,
            DocumentRuntimeSerialNumber = source.DocumentRuntimeSerialNumber,
            DocumentPath = source.DocumentPath,
            ObjectName = source.ObjectName,
            LayerFullPath = source.LayerFullPath,
            SystemCode = source.SystemCode,
            GeometryFingerprint = source.GeometryFingerprint,
            GeometryClass = projected.Classification,
            GeometryDiagnostic = projected.Diagnostic,
            Width = source.Width,
            Height = source.Height,
            ModelTolerance = source.ModelTolerance,
            ModelUnitScaleToMillimeters = source.ModelUnitScaleToMillimeters,
            HorizontalOffsets = keySet.HorizontalOffsets,
            VerticalOffsets = keySet.VerticalOffsets,
            Cells = keySet.Cells,
            Preview = projected.Preview,
            WorkbookPath = source.WorkbookPath
        });
    }

    private static bool OffsetsEqual(
        IReadOnlyList<double> left,
        IReadOnlyList<double> right,
        double tolerance)
    {
        return left.Count == right.Count && left
            .Zip(right, (leftValue, rightValue) => Math.Abs(leftValue - rightValue) <= tolerance)
            .All(equal => equal);
    }

    private static ObjectEnumeratorSettings CreateSurfaceEnumeratorSettings()
    {
        return new ObjectEnumeratorSettings
        {
            NormalObjects = true,
            LockedObjects = true,
            HiddenObjects = true,
            ActiveObjects = true,
            ReferenceObjects = false,
            ObjectTypeFilter = ObjectType.Brep
        };
    }

    private static void SelectSkippedPanels(RhinoDoc document, IReadOnlyList<Guid> skippedPanelIds)
    {
        Guid[] distinctIds = (skippedPanelIds ?? Array.Empty<Guid>())
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (distinctIds.Length == 0)
        {
            return;
        }

        document.Objects.UnselectAll();
        document.Objects.Select(distinctIds);
        document.Views.Redraw();
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

    private static IReadOnlyDictionary<string, string> ReadUserText(RhinoObject rhinoObject)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = rhinoObject.Attributes.GetUserStrings();
        foreach (string? key in values?.AllKeys ?? Array.Empty<string?>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = values?[key] ?? string.Empty;
            }
        }
        return result;
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
        string root = (layerPath ?? string.Empty).Split(
            new[] { "::" },
            StringSplitOptions.None)[0];
        return PanelCladdingSpawnPlanningService.IsSupportedMaterialSurfaceRoot(root);
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

    private sealed record PanelReadCandidate(
        RhinoObject Object,
        Brep Geometry,
        string PanelId,
        PanelCladdingLayout Layout,
        IReadOnlyDictionary<string, string> UserText);

    private sealed record SurfaceReadCandidate(
        RhinoObject Object,
        Brep Geometry,
        string PanelId,
        string Cid,
        string LayerPath,
        string CladdingValue);
}
