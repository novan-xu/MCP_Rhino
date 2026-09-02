extern alias rhinocommon;

using System.Globalization;
using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
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
    private readonly PanelCladdingLayoutReconciliationService _reconciliation;
    private readonly PanelCladdingSurfaceCoverageService _surfaceCoverage;

    public LivePanelCladdingSurfaceSyncRepository(
        ILivePanelCladdingRepository layouts,
        PanelCladdingKeyService keys,
        PanelAxonometricProjectionService projection)
    {
        _layouts = layouts;
        _keys = keys;
        _projection = projection;
        _reconciliation = new PanelCladdingLayoutReconciliationService();
        _surfaceCoverage = new PanelCladdingSurfaceCoverageService();
    }

    public OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingObjectScope scope)
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

        var discoveredSurfaces = new List<SurfaceReadCandidate>();
        var discoveredCurves = new List<CurveReadCandidate>();
        var modelTypeAssignments = new List<PanelCladdingWorkbookTypeReference>();
        foreach (RhinoObject rhinoObject in document.Objects.GetObjectList(
            CreateSyncObjectEnumeratorSettings()))
        {
            if (rhinoObject.Geometry is Brep)
            {
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
            }
            if (selectedIds.Contains(rhinoObject.Id))
            {
                continue;
            }
            string layerPath = GetLayerPath(document, rhinoObject);
            if (scope == PanelCladdingObjectScope.Surfaces &&
                rhinoObject.Geometry is Brep surfaceGeometry && IsUnderMaterialSurfaceRoot(layerPath))
            {
                discoveredSurfaces.Add(new SurfaceReadCandidate(
                    rhinoObject,
                    surfaceGeometry,
                    GetCanonicalUserText(rhinoObject.Attributes, PanelCladdingSpawnPlanningService.PanelIdUserTextKey),
                    GetCanonicalUserText(rhinoObject.Attributes, PanelCladdingSpawnPlanningService.CidUserTextKey),
                    layerPath,
                    GetCanonicalUserText(rhinoObject.Attributes, CladdingUserTextKey),
                    GetCanonicalUserText(
                        rhinoObject.Attributes,
                        PanelCladdingSurfaceCoverageService.UserTextKey),
                    GetCanonicalUserText(
                        rhinoObject.Attributes,
                        PanelCladdingSurfaceCoverageService.LegacyUserTextKey)));
                continue;
            }
            if (rhinoObject.Geometry is Curve curveGeometry && string.Equals(
                layerPath,
                PanelCladdingExtrusionPlanningService.CurveLayerPath,
                StringComparison.OrdinalIgnoreCase))
            {
                discoveredCurves.Add(new CurveReadCandidate(
                    rhinoObject,
                    curveGeometry,
                    GetCanonicalUserText(rhinoObject.Attributes, PanelCladdingSpawnPlanningService.PanelIdUserTextKey),
                    GetCanonicalUserText(rhinoObject.Attributes, PanelCladdingSpawnPlanningService.CidUserTextKey),
                    GetCanonicalUserText(rhinoObject.Attributes, PanelCladdingExtrusionPlanningService.CurveUserTextKey),
                    GetCanonicalUserText(
                        rhinoObject.Attributes,
                        PanelCladdingExtrusionPlanningService.AssignedExtrusionsUserTextKey),
                    ReadExtrusionValues(rhinoObject.Attributes),
                    layerPath));
            }
        }

        Dictionary<Guid, Guid> surfaceOwners = ResolveSurfaceOwners();
        Dictionary<Guid, Guid> curveOwners = ResolveCurveOwners();
        var panels = new List<PanelCladdingSurfaceSyncPanelSnapshot>(candidates.Count);
        var surfaces = new List<PanelCladdingSurfaceSyncSurfaceSnapshot>();
        var curves = new List<PanelCladdingSurfaceSyncCurveSnapshot>();
        foreach (PanelReadCandidate panel in candidates)
        {
            string pid = panel.PanelId.Trim();
            string panelCid = GetCanonicalUserText(
                panel.Object.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
            if (pid.Length == 0 || panelCid.Length == 0)
            {
                issues.Add(new PanelCladdingSurfaceSyncIssue
                {
                    PanelObjectId = panel.Object.Id,
                    PanelId = pid,
                    Message = pid.Length == 0
                        ? $"PANEL_CLADDING_SURFACE_SYNC_PID_REQUIRED: {panel.Object.Id:D}"
                        : $"PANEL_CLADDING_SURFACE_SYNC_CID_REQUIRED: {panel.Object.Id:D}"
                });
                panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = panel.Object.Id,
                    PanelId = panel.PanelId,
                    PanelCid = panelCid,
                    Layout = panel.Layout
                });
                continue;
            }

            SurfaceReadCandidate[] panelSurfaces = discoveredSurfaces
                .Where(surface => surfaceOwners.TryGetValue(surface.Object.Id, out Guid owner) &&
                    owner == panel.Object.Id)
                .ToArray();
            CurveReadCandidate[] panelCurves = discoveredCurves
                .Where(curve => curveOwners.TryGetValue(curve.Object.Id, out Guid owner) &&
                    owner == panel.Object.Id)
                .ToArray();
            if (scope == PanelCladdingObjectScope.Surfaces && panelSurfaces.Length == 0)
            {
                issues.Add(new PanelCladdingSurfaceSyncIssue
                {
                    PanelObjectId = panel.Object.Id,
                    PanelId = pid,
                    Message = $"PANEL_CLADDING_SURFACE_SYNC_SURFACE_MISSING: {pid}: no geometrically associated Breps found under a supported material root."
                });
                panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
                {
                    ObjectId = panel.Object.Id,
                    PanelId = panel.PanelId,
                    PanelCid = panelCid,
                    Layout = panel.Layout
                });
                continue;
            }
            if (scope == PanelCladdingObjectScope.Curves && panelCurves.Length == 0)
            {
                AddPanelIssue(panel, $"PANEL_CLADDING_SYNC_CURVES_MISSING: {pid}");
                continue;
            }

            var coverageBySurfaceId = new Dictionary<Guid, string>();
            OperationResponse<PanelCladdingInferredOffsets> effectiveOffsets;
            if (scope == PanelCladdingObjectScope.Surfaces)
            {
                IReadOnlyList<IReadOnlyList<string>>? storedSurfaceCoverages = null;
                string? coverageKeyIssue = null;
                foreach (SurfaceReadCandidate surface in panelSurfaces)
                {
                    OperationResponse<string> storedCoverage = _surfaceCoverage.ResolveStoredValue(
                        surface.CoverageValue,
                        surface.LegacyCoverageValue);
                    if (!storedCoverage.Success || storedCoverage.Data is null)
                    {
                        coverageKeyIssue = $"{storedCoverage.Message} {surface.Object.Id:D}";
                        break;
                    }
                    coverageBySurfaceId[surface.Object.Id] = storedCoverage.Data;
                }
                if (coverageKeyIssue is not null)
                {
                    AddPanelIssue(panel, coverageKeyIssue);
                    continue;
                }
                int coverageValueCount = panelSurfaces.Count(surface =>
                    !string.IsNullOrWhiteSpace(coverageBySurfaceId[surface.Object.Id]));
                if (coverageValueCount > 0 && coverageValueCount != panelSurfaces.Length)
                {
                    AddPanelIssue(panel,
                        $"PANEL_CLADDING_SURFACE_COVERAGE_INCOMPLETE: {pid}: " +
                        $"{coverageValueCount}/{panelSurfaces.Length} surfaces have " +
                        $"{PanelCladdingSurfaceCoverageService.UserTextKey}.");
                    continue;
                }
                if (coverageValueCount == panelSurfaces.Length)
                {
                    var decodedCoverages = new List<IReadOnlyList<string>>(panelSurfaces.Length);
                    string? coverageIssue = null;
                    foreach (SurfaceReadCandidate surface in panelSurfaces)
                    {
                        OperationResponse<IReadOnlyList<string>> decoded =
                            _surfaceCoverage.Decode(coverageBySurfaceId[surface.Object.Id]);
                        if (!decoded.Success || decoded.Data is null)
                        {
                            coverageIssue = $"PANEL_CLADDING_SURFACE_COVERAGE_INVALID: " +
                                $"{surface.Object.Id:D}: {decoded.Message}";
                            break;
                        }
                        decodedCoverages.Add(decoded.Data);
                    }
                    if (coverageIssue is not null)
                    {
                        AddPanelIssue(panel, coverageIssue);
                        continue;
                    }
                    storedSurfaceCoverages = decodedCoverages;
                }
                OperationResponse<PanelCladdingInferredOffsets> surfaceInferred =
                    LivePanelCladdingGeometryPartitionService.InferOffsets(
                        panel.Geometry,
                        panelSurfaces.Select(surface => surface.Geometry).ToArray(),
                        Array.Empty<Curve>(),
                        panel.Layout.ModelTolerance);
                if (!surfaceInferred.Success || surfaceInferred.Data is null)
                {
                    AddPanelIssue(panel,
                        $"PANEL_CLADDING_SURFACE_SYNC_OFFSET_INFERENCE_FAILED: {surfaceInferred.Message}");
                    continue;
                }
                PanelCladdingInferredOffsets? curveInferredData = null;
                if (panelCurves.Length > 0)
                {
                    OperationResponse<PanelCladdingInferredOffsets> curveInferred =
                        LivePanelCladdingGeometryPartitionService.InferOffsets(
                            panel.Geometry,
                            Array.Empty<Brep>(),
                            panelCurves.Select(curve => curve.Geometry).ToArray(),
                            panel.Layout.ModelTolerance);
                    if (!curveInferred.Success || curveInferred.Data is null)
                    {
                        AddPanelIssue(panel,
                            $"PANEL_CLADDING_SURFACE_SYNC_CURVE_OFFSET_INFERENCE_FAILED: {curveInferred.Message}");
                        continue;
                    }
                    curveInferredData = curveInferred.Data;
                }
                effectiveOffsets = PanelCladdingSurfaceSyncPlanningService.ResolveSurfaceScopeOffsets(
                    panel.Layout,
                    surfaceInferred.Data,
                    curveInferredData,
                    storedSurfaceCoverages);
            }
            else
            {
                OperationResponse<PanelCladdingInferredOffsets> inferred =
                    LivePanelCladdingGeometryPartitionService.InferOffsets(
                        panel.Geometry,
                        Array.Empty<Brep>(),
                        panelCurves.Select(curve => curve.Geometry).ToArray(),
                        panel.Layout.ModelTolerance);
                if (!inferred.Success || inferred.Data is null)
                {
                    AddPanelIssue(panel,
                        $"PANEL_CLADDING_SURFACE_SYNC_OFFSET_INFERENCE_FAILED: {inferred.Message}");
                    continue;
                }
                effectiveOffsets = PanelCladdingSurfaceSyncPlanningService.ResolveEffectiveOffsets(
                    scope,
                    panel.Layout.HorizontalOffsets,
                    panel.Layout.VerticalOffsets,
                    inferred.Data,
                    panel.Layout.Width,
                    panel.Layout.Height,
                    panel.Layout.ModelTolerance);
            }
            if (!effectiveOffsets.Success || effectiveOffsets.Data is null)
            {
                AddPanelIssue(panel, effectiveOffsets.Message);
                continue;
            }
            IReadOnlyList<double> horizontalOffsets = effectiveOffsets.Data.HorizontalOffsets;
            IReadOnlyList<double> verticalOffsets = effectiveOffsets.Data.VerticalOffsets;
            if (scope == PanelCladdingObjectScope.Curves && panel.Layout.Topology.HiddenSegments.Count > 0)
            {
                horizontalOffsets = PreserveHiddenTrackOffsets(
                    PanelCladdingTopologyAxis.Horizontal,
                    panel.Layout.HorizontalOffsets,
                    horizontalOffsets,
                    panel.Layout.Topology.HiddenSegments,
                    panel.Layout.ModelTolerance);
                verticalOffsets = PreserveHiddenTrackOffsets(
                    PanelCladdingTopologyAxis.Vertical,
                    panel.Layout.VerticalOffsets,
                    verticalOffsets,
                    panel.Layout.Topology.HiddenSegments,
                    panel.Layout.ModelTolerance);
            }
            PanelCladdingInferredExtrusionLayout? inferredExtrusions = null;
            PanelCladdingLayoutReconciliationResult? layoutReconciliation = null;
            PanelCladdingTopologyState topology;
            if (scope == PanelCladdingObjectScope.Curves)
            {
                OperationResponse<PanelCladdingInferredExtrusionLayout> extrusionResponse =
                    LivePanelCladdingGeometryPartitionService.InferExtrusionTopology(
                        panel.Geometry,
                        panelCurves.Select(curve => curve.Geometry).ToArray(),
                        horizontalOffsets,
                        verticalOffsets,
                        panel.Layout.ModelTolerance);
                if (!extrusionResponse.Success || extrusionResponse.Data is null)
                {
                    AddPanelIssue(panel, $"PANEL_CLADDING_SYNC_TOPOLOGY_INFERENCE_FAILED: {extrusionResponse.Message}");
                    continue;
                }
                inferredExtrusions = extrusionResponse.Data;
                topology = RestoreExplicitHiddenSegments(
                    extrusionResponse.Data.Topology,
                    panel.Layout.Topology,
                    panel.Layout.HorizontalOffsets.Count == horizontalOffsets.Count &&
                    panel.Layout.VerticalOffsets.Count == verticalOffsets.Count);
            }
            else
            {
                OperationResponse<PanelCladdingLayoutReconciliationResult> reconciliation =
                    _reconciliation.Reconcile(
                        panel.Layout.HorizontalOffsets,
                        panel.Layout.VerticalOffsets,
                        horizontalOffsets,
                        verticalOffsets,
                        panel.Layout.Topology,
                        panel.Layout.Width,
                        panel.Layout.Height,
                        panel.Layout.ModelTolerance);
                if (!reconciliation.Success || reconciliation.Data is null)
                {
                    AddPanelIssue(panel,
                        $"PANEL_CLADDING_SURFACE_SYNC_RECONCILIATION_FAILED: {reconciliation.Message}");
                    continue;
                }
                layoutReconciliation = reconciliation.Data;
                topology = layoutReconciliation.Topology;
                if (panelCurves.Length > 0)
                {
                    OperationResponse<PanelCladdingInferredExtrusionLayout> extrusionResponse =
                        LivePanelCladdingGeometryPartitionService.InferExtrusionTopology(
                            panel.Geometry,
                            panelCurves.Select(curve => curve.Geometry).ToArray(),
                            horizontalOffsets,
                            verticalOffsets,
                            panel.Layout.ModelTolerance);
                    if (!extrusionResponse.Success || extrusionResponse.Data is null)
                    {
                        AddPanelIssue(panel,
                            $"PANEL_CLADDING_SYNC_CURVE_REINDEX_FAILED: {extrusionResponse.Message}");
                        continue;
                    }
                    inferredExtrusions = extrusionResponse.Data;
                }
            }
            OperationResponse<PanelCladdingKeySet> baseKeySet = _keys.CreateKeySet(
                horizontalOffsets,
                verticalOffsets,
                scope == PanelCladdingObjectScope.Curves
                    ? panel.UserText
                    : new Dictionary<string, string>(),
                panel.Layout.Width,
                panel.Layout.Height,
                panel.Layout.ModelTolerance);
            if (!baseKeySet.Success || baseKeySet.Data is null)
            {
                AddPanelIssue(panel, $"PANEL_CLADDING_SURFACE_SYNC_INFERRED_GRID_INVALID: {baseKeySet.Message}");
                continue;
            }
            PanelCladdingKeySet keySet = new()
            {
                HorizontalOffsets = baseKeySet.Data.HorizontalOffsets,
                VerticalOffsets = baseKeySet.Data.VerticalOffsets,
                Cells = baseKeySet.Data.Cells,
                Topology = topology,
                FrameAssignments = scope == PanelCladdingObjectScope.Curves
                    ? baseKeySet.Data.FrameAssignments
                    : panel.Layout.FrameAssignments
            };
            OperationResponse<PanelCladdingLayout> inferredLayout = BuildInferredLayout(
                panel.Layout,
                keySet);
            if (!inferredLayout.Success || inferredLayout.Data is null)
            {
                AddPanelIssue(panel, inferredLayout.Message);
                continue;
            }
            OperationResponse<LivePanelCladdingGeometryGrid> geometryGrid =
                LivePanelCladdingGeometryPartitionService.CreateGrid(
                    panel.Geometry,
                    keySet,
                    panel.Layout.ModelTolerance);
            if (!geometryGrid.Success || geometryGrid.Data is null)
            {
                AddPanelIssue(panel,
                    $"PANEL_CLADDING_SURFACE_SYNC_GRID_FAILED: {panel.Object.Id:D}: {geometryGrid.Message}");
                continue;
            }

            using LivePanelCladdingGeometryGrid grid = geometryGrid.Data;
            var surfaceOwnerByCellLabel = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
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
                    string surfaceOwner = surface.Object.Id.ToString("D");
                    foreach (string label in coveredCellLabels)
                    {
                        surfaceOwnerByCellLabel.TryAdd(label, surfaceOwner);
                    }
                }
                surfaces.Add(new PanelCladdingSurfaceSyncSurfaceSnapshot
                {
                    ObjectId = surface.Object.Id,
                    PanelObjectId = panel.Object.Id,
                    PanelId = surface.PanelId,
                    Cid = surface.Cid,
                    LayerPath = surface.LayerPath,
                    CladdingValue = surface.CladdingValue,
                    CoverageValue = coverageBySurfaceId.GetValueOrDefault(
                        surface.Object.Id,
                        string.Empty),
                    LegacyCoverageValue = surface.LegacyCoverageValue,
                    CoveredCellLabels = coveredCellLabels
                });
            }

            if (scope == PanelCladdingObjectScope.Surfaces &&
                layoutReconciliation is not null)
            {
                topology = PanelCladdingLayoutReconciliationService.ApplyInsertedTrackEvidence(
                    layoutReconciliation,
                    topology,
                    surfaceOwnerByCellLabel,
                    inferredExtrusions?.Topology,
                    horizontalOffsets.Count,
                    verticalOffsets.Count);
                keySet = new PanelCladdingKeySet
                {
                    HorizontalOffsets = keySet.HorizontalOffsets,
                    VerticalOffsets = keySet.VerticalOffsets,
                    Cells = keySet.Cells,
                    Topology = topology,
                    FrameAssignments = keySet.FrameAssignments
                };
                inferredLayout = BuildInferredLayout(panel.Layout, keySet);
                if (!inferredLayout.Success || inferredLayout.Data is null)
                {
                    AddPanelIssue(panel, inferredLayout.Message);
                    continue;
                }
            }

            if (inferredExtrusions is not null)
            {
                var extrusionPlanner = new PanelCladdingExtrusionPlanningService();
                OperationResponse<IReadOnlyList<PanelCladdingExtrusionCurvePlan>> expectedCurves =
                    extrusionPlanner.CreatePlan(
                    pid,
                    panelCid,
                    inferredLayout.Data.Width,
                    inferredLayout.Data.Height,
                    keySet);
                if (!expectedCurves.Success || expectedCurves.Data is null)
                {
                    AddPanelIssue(panel, expectedCurves.Message);
                    continue;
                }
                bool curveMappingFailed = false;
                foreach (PanelCladdingInferredCurveGeometry inferredCurve in inferredExtrusions.Curves)
                {
                    PanelCladdingExtrusionCurvePlan? expected = expectedCurves.Data.FirstOrDefault(item =>
                        inferredCurve.FrameCode.Length > 0
                            ? string.Equals(item.Code, inferredCurve.FrameCode, StringComparison.Ordinal)
                            : item.AtomicSegments.ToHashSet().SetEquals(inferredCurve.AtomicSegments));
                    if (expected is null || inferredCurve.SourceIndex < 0 || inferredCurve.SourceIndex >= panelCurves.Length)
                    {
                        AddPanelIssue(panel, $"PANEL_CLADDING_SYNC_CURVE_PLAN_NOT_FOUND: {inferredCurve.SourceIndex}");
                        curveMappingFailed = true;
                        break;
                    }
                    CurveReadCandidate sourceCurve = panelCurves[inferredCurve.SourceIndex];
                    curves.Add(new PanelCladdingSurfaceSyncCurveSnapshot
                    {
                        ObjectId = sourceCurve.Object.Id,
                        PanelObjectId = panel.Object.Id,
                        PanelId = sourceCurve.PanelId,
                        Cid = sourceCurve.Cid,
                        CurveCode = sourceCurve.CurveCode,
                        DesiredCode = expected.Code,
                        LayerPath = sourceCurve.LayerPath,
                        AssignedExtrusions = sourceCurve.AssignedExtrusions,
                        DesiredAssignedExtrusions = string.Join(';', expected.AssignedExtrusionCodes),
                        AssignedExtrusionValues = sourceCurve.AssignedExtrusionValues,
                        DesiredAssignedExtrusionValues = expected.AssignedExtrusionValues
                    });
                }
                if (curveMappingFailed)
                {
                    continue;
                }
            }

            OperationResponse<IReadOnlyDictionary<string, string>> inferredMasks =
                _keys.EncodeNonDefaultTopology(
                    keySet.Topology,
                    keySet.HorizontalOffsets.Count,
                    keySet.VerticalOffsets.Count);
            if (!inferredMasks.Success || inferredMasks.Data is null)
            {
                AddPanelIssue(panel, inferredMasks.Message);
                continue;
            }
            panel.UserText.TryGetValue(PanelCladdingKeyService.UnitWidthKey, out string? storedWidth);
            panel.UserText.TryGetValue(PanelCladdingKeyService.UnitHeightKey, out string? storedHeight);
            panel.UserText.TryGetValue(PanelCladdingKeyService.UnitDimensionKey, out string? storedDimension);
            string expectedWidth = PanelCladdingKeyService.FormatUnitDimension(inferredLayout.Data.Width);
            string expectedHeight = PanelCladdingKeyService.FormatUnitDimension(inferredLayout.Data.Height);
            bool gridChanged = !OffsetsEqual(
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
                    inferredLayout.Data.VerticalOffsets) ||
                !PanelCladdingKeyService.TopologyPersistenceMatches(
                    panel.UserText,
                    inferredMasks.Data) ||
                !string.Equals(storedWidth, expectedWidth, StringComparison.Ordinal) ||
                !string.Equals(storedHeight, expectedHeight, StringComparison.Ordinal) ||
                !string.Equals(storedDimension, $"{expectedWidth}x{expectedHeight}", StringComparison.Ordinal) ||
                panel.UserText.Keys.Any(key =>
                    string.Equals(
                        key,
                        PanelCladdingKeyService.SignatureKey,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        key,
                        PanelCladdingKeyService.LegacySignatureKey,
                        StringComparison.OrdinalIgnoreCase));

            panels.Add(new PanelCladdingSurfaceSyncPanelSnapshot
            {
                ObjectId = panel.Object.Id,
                PanelId = panel.PanelId,
                PanelCid = panelCid,
                Layout = inferredLayout.Data,
                GridChanged = gridChanged
            });
        }
        return OperationResponse<PanelCladdingSurfaceSyncSnapshot>.Ok(
            new PanelCladdingSurfaceSyncSnapshot
            {
                Scope = scope,
                DocumentPath = document.Path,
                WorkbookPath = document.Strings.GetValue(
                    PanelCladdingKeyService.WorkbookPathDocumentKey) ?? string.Empty,
                SelectedPanelIds = selectedIds,
                Panels = panels,
                Surfaces = surfaces,
                Curves = curves,
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
                PanelCid = GetCanonicalUserText(panel.Object.Attributes, PanelCladdingSpawnPlanningService.CidUserTextKey),
                Layout = panel.Layout
            });
        }

        Dictionary<Guid, Guid> ResolveSurfaceOwners()
        {
            var owners = new Dictionary<Guid, Guid>();
            foreach (SurfaceReadCandidate surface in discoveredSurfaces)
            {
                PanelReadCandidate[] matches = candidates.Where(panel =>
                    LivePanelCladdingGeometryPartitionService.IsAssociated(
                        panel.Geometry,
                        surface.Geometry,
                        panel.Layout.ModelTolerance)).ToArray();
                if (matches.Length == 1)
                {
                    owners[surface.Object.Id] = matches[0].Object.Id;
                }
                else if (matches.Length > 1)
                {
                    foreach (PanelReadCandidate panel in matches)
                    {
                        issues.Add(new PanelCladdingSurfaceSyncIssue
                        {
                            PanelObjectId = panel.Object.Id,
                            PanelId = panel.PanelId,
                            Message = $"PANEL_CLADDING_SYNC_AMBIGUOUS_SURFACE_OWNER: {surface.Object.Id:D}"
                        });
                    }
                }
            }
            return owners;
        }

        Dictionary<Guid, Guid> ResolveCurveOwners()
        {
            var owners = new Dictionary<Guid, Guid>();
            foreach (CurveReadCandidate curve in discoveredCurves)
            {
                PanelReadCandidate[] matches = candidates.Where(panel =>
                    LivePanelCladdingGeometryPartitionService.IsAssociated(
                        panel.Geometry,
                        curve.Geometry,
                        panel.Layout.ModelTolerance)).ToArray();
                if (matches.Length == 1)
                {
                    owners[curve.Object.Id] = matches[0].Object.Id;
                }
                else if (matches.Length > 1)
                {
                    foreach (PanelReadCandidate panel in matches)
                    {
                        issues.Add(new PanelCladdingSurfaceSyncIssue
                        {
                            PanelObjectId = panel.Object.Id,
                            PanelId = panel.PanelId,
                            Message = $"PANEL_CLADDING_SYNC_AMBIGUOUS_CURVE_OWNER: {curve.Object.Id:D}"
                        });
                    }
                }
            }
            return owners;
        }
    }

    public OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
        PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook,
        IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount,
        int matchedCurveCount)
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
        PanelCladdingSurfaceSyncCurvePlan[] curveWrites = (request.CurveWrites ??
                Array.Empty<PanelCladdingSurfaceSyncCurvePlan>())
            .GroupBy(item => item.ObjectId)
            .Select(group => group.First())
            .ToArray();
        var prepared = new List<PreparedObject>(surfaceWrites.Length + curveWrites.Length + panelWrites.Length);

        foreach (PanelCladdingSurfaceSyncSurfacePlan surfaceWrite in surfaceWrites)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(surfaceWrite.ObjectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.ExpectedCid}: surface not found");
            }
            string currentLayerPath = GetLayerPath(document, rhinoObject);
            if (!string.Equals(currentLayerPath, surfaceWrite.ExpectedLayerPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_STALE_SURFACE: {surfaceWrite.ExpectedCid}: layer changed");
            }

            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, CladdingUserTextKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingSpawnPlanningService.CidUserTextKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            DeleteUserTextCaseInsensitive(
                proposed,
                PanelCladdingSurfaceCoverageService.UserTextKey);
            DeleteUserTextCaseInsensitive(
                proposed,
                PanelCladdingSurfaceCoverageService.LegacyUserTextKey);
            proposed.SetUserString(CladdingUserTextKey, surfaceWrite.MaterialCode);
            proposed.SetUserString(
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey,
                surfaceWrite.PanelId);
            proposed.SetUserString(
                PanelCladdingSpawnPlanningService.CidUserTextKey,
                surfaceWrite.DesiredCid);
            proposed.SetUserString(
                PanelCladdingSurfaceCoverageService.UserTextKey,
                surfaceWrite.DesiredCoverageValue);
            proposed.Name = surfaceWrite.DesiredCid;
            prepared.Add(new PreparedObject(rhinoObject, original, proposed, null));
        }

        foreach (PanelCladdingSurfaceSyncCurvePlan curveWrite in curveWrites)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(curveWrite.ObjectId);
            if (rhinoObject?.Geometry is not Curve)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SYNC_STALE_CURVE: {curveWrite.ObjectId:D}: curve not found");
            }
            string currentLayerPath = GetLayerPath(document, rhinoObject);
            if (!string.Equals(currentLayerPath, curveWrite.ExpectedLayerPath, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SYNC_STALE_CURVE: {curveWrite.ObjectId:D}: layer changed");
            }
            ObjectAttributes original = rhinoObject.Attributes.Duplicate();
            ObjectAttributes proposed = rhinoObject.Attributes.Duplicate();
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingSpawnPlanningService.PanelIdUserTextKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingSpawnPlanningService.CidUserTextKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingExtrusionPlanningService.CurveUserTextKey);
            DeleteUserTextCaseInsensitive(
                proposed,
                PanelCladdingExtrusionPlanningService.AssignedExtrusionsUserTextKey);
            foreach (string? key in proposed.GetUserStrings()?.AllKeys ?? Array.Empty<string?>())
            {
                if (PanelFrameAssignmentService.IsExtrusionCode(key))
                {
                    proposed.DeleteUserString(key!);
                }
            }
            proposed.SetUserString(PanelCladdingSpawnPlanningService.PanelIdUserTextKey, curveWrite.PanelId);
            proposed.SetUserString(PanelCladdingSpawnPlanningService.CidUserTextKey, curveWrite.DesiredCid);
            proposed.SetUserString(PanelCladdingExtrusionPlanningService.CurveUserTextKey, curveWrite.DesiredCode);
            if (!string.IsNullOrWhiteSpace(curveWrite.DesiredAssignedExtrusions))
            {
                proposed.SetUserString(
                    PanelCladdingExtrusionPlanningService.AssignedExtrusionsUserTextKey,
                    curveWrite.DesiredAssignedExtrusions);
            }
            foreach ((string code, string value) in curveWrite.DesiredAssignedExtrusionValues)
            {
                proposed.SetUserString(code, value);
            }
            proposed.Name = curveWrite.DesiredCode;
            prepared.Add(new PreparedObject(rhinoObject, original, proposed, null));
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
            OperationResponse<IReadOnlyDictionary<string, string>> topology =
                _keys.EncodeNonDefaultTopology(
                    panelWrite.Topology,
                    panelWrite.HorizontalOffsets.Count,
                    panelWrite.VerticalOffsets.Count);
            if (!topology.Success || topology.Data is null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_TOPOLOGY_INVALID: {panelWrite.ObjectId:D}: {topology.Message}");
            }
            string?[] existingKeys = proposed.GetUserStrings()?.AllKeys ?? Array.Empty<string?>();
            foreach (string? key in existingKeys)
            {
                if (key is not null && (_keys.IsOffsetKey(key) || _keys.IsCladdingCellKey(key) ||
                    PanelCladdingKeyService.IsTopologyKey(key)))
                {
                    proposed.DeleteUserString(key);
                }
            }
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacyTypeCodeKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.SignatureKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.LegacySignatureKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.UnitWidthKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.UnitHeightKey);
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.UnitDimensionKey);
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
            DeleteUserTextCaseInsensitive(proposed, PanelCladdingKeyService.CladdingLogicKey);
            proposed.SetUserString(
                PanelCladdingKeyService.CladdingLogicKey,
                panelWrite.CladdingLogic);
            proposed.SetUserString(PanelCladdingKeyService.TypeCodeKey, panelWrite.TypeCode);
            foreach ((string key, string value) in topology.Data)
            {
                proposed.SetUserString(key, value);
            }
            proposed.SetUserString(PanelCladdingKeyService.UnitWidthKey, panelWrite.UnitWidth);
            proposed.SetUserString(PanelCladdingKeyService.UnitHeightKey, panelWrite.UnitHeight);
            proposed.SetUserString(PanelCladdingKeyService.UnitDimensionKey, panelWrite.UnitDimension);
            prepared.Add(new PreparedObject(rhinoObject, original, proposed, panelWrite));
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
                matchedSurfaceCount,
                matchedCurveCount));
        }

        string? oldWorkbookPath = document.Strings.GetValue(
            PanelCladdingKeyService.WorkbookPathDocumentKey);
        bool workbookPathChanged = panelWrites.Length > 0 &&
            !string.IsNullOrWhiteSpace(request.WorkbookPath) &&
            !string.Equals(oldWorkbookPath, request.WorkbookPath, StringComparison.OrdinalIgnoreCase);
        uint undoRecord = document.BeginUndoRecord(
            request.Scope == PanelCladdingObjectScope.Surfaces
                ? "Sync Panel Cladding Surfaces"
                : "Sync Panel Cladding Curves");
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
                if (item.PanelWrite is not null)
                {
                    RhinoObject? committed = document.Objects.FindId(item.Object.Id);
                    OperationResponse verified = committed is null
                        ? OperationResponse.Fail("PANEL_CLADDING_SURFACE_SYNC_PANEL_MISSING_AFTER_COMMIT")
                        : PanelCladdingSurfaceSyncService.ValidateAppliedPanelCellValues(
                            ReadUserText(committed),
                            item.PanelWrite);
                    if (!verified.Success)
                    {
                        bool rollbackSucceeded = RestoreOriginalAttributes(document, modified);
                        RestoreDocumentString(document, oldWorkbookPath);
                        string rollback = rollbackSucceeded ? string.Empty : ": ROLLBACK_FAILED";
                        return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                            $"PANEL_CLADDING_SURFACE_SYNC_ATTRIBUTE_VERIFY_FAILED: " +
                            $"{item.Object.Id:D}: {verified.Message}{rollback}");
                    }
                }
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
                matchedSurfaceCount,
                matchedCurveCount));
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
        int matchedSurfaceCount,
        int matchedCurveCount)
    {
        return new PanelCladdingSurfaceSyncResult
        {
            Scope = request.Scope,
            SelectedPanelIds = request.SelectedPanelIds,
            SkippedPanelIds = request.SkippedPanelIds,
            ChangedPanelIds = panelWrites.Select(item => item.ObjectId).ToArray(),
            RefreshedSurfaceIds = surfaceWrites.Select(item => item.ObjectId).ToArray(),
            RefreshedCurveIds = request.CurveWrites.Select(item => item.ObjectId).ToArray(),
            MatchedSurfaceCount = matchedSurfaceCount,
            MatchedCurveCount = matchedCurveCount,
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
            Topology = keySet.Topology,
            FrameAssignments = keySet.FrameAssignments,
            SourceUserText = source.SourceUserText,
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

    private static IReadOnlyList<double> PreserveHiddenTrackOffsets(
        PanelCladdingTopologyAxis axis,
        IReadOnlyList<double> storedOffsets,
        IReadOnlyList<double> inferredOffsets,
        IReadOnlyList<PanelCladdingSegmentCoordinate> hiddenSegments,
        double tolerance)
    {
        double duplicateTolerance = Math.Max(tolerance * 10d, 1e-5d);
        var result = inferredOffsets.ToList();
        foreach (int track in hiddenSegments
            .Where(segment => segment.Axis == axis)
            .Select(segment => segment.Track)
            .Distinct()
            .OrderBy(track => track))
        {
            if (track < 0 || track >= storedOffsets.Count)
            {
                continue;
            }
            double offset = storedOffsets[track];
            if (!result.Any(candidate => Math.Abs(candidate - offset) <= duplicateTolerance))
            {
                result.Add(offset);
            }
        }
        return result.OrderBy(value => value).ToArray();
    }

    private static PanelCladdingTopologyState RestoreExplicitHiddenSegments(
        PanelCladdingTopologyState inferred,
        PanelCladdingTopologyState stored,
        bool gridsMapByIndex)
    {
        if (!gridsMapByIndex || stored.HiddenSegments.Count == 0)
        {
            return inferred;
        }
        HashSet<PanelCladdingSegmentCoordinate> inferredMissing = inferred.MissingSegments.ToHashSet();
        PanelCladdingSegmentCoordinate[] hidden = stored.HiddenSegments
            .Where(inferredMissing.Contains)
            .Distinct()
            .OrderBy(segment => segment.Axis)
            .ThenBy(segment => segment.Track)
            .ThenBy(segment => segment.Bay)
            .ToArray();
        if (hidden.Length == 0)
        {
            return inferred;
        }
        HashSet<PanelCladdingSegmentCoordinate> hiddenSet = hidden.ToHashSet();
        return new PanelCladdingTopologyState
        {
            MissingSegments = inferred.MissingSegments.Where(segment => !hiddenSet.Contains(segment)).ToArray(),
            HiddenSegments = hidden,
            MergeRuns = inferred.MergeRuns
        };
    }

    private static ObjectEnumeratorSettings CreateSyncObjectEnumeratorSettings()
    {
        return new ObjectEnumeratorSettings
        {
            NormalObjects = true,
            LockedObjects = true,
            HiddenObjects = true,
            ActiveObjects = true,
            ReferenceObjects = false,
            ObjectTypeFilter = ObjectType.Brep | ObjectType.Curve
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

    private static IReadOnlyDictionary<string, string> ReadExtrusionValues(ObjectAttributes attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = attributes.GetUserStrings();
        foreach (string? key in values?.AllKeys ?? Array.Empty<string?>())
        {
            if (PanelFrameAssignmentService.IsExtrusionCode(key))
            {
                result[key!] = values?[key!] ?? string.Empty;
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
        ObjectAttributes Proposed,
        PanelCladdingSurfaceSyncPanelWrite? PanelWrite);

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
        string CladdingValue,
        string CoverageValue,
        string LegacyCoverageValue);

    private sealed record CurveReadCandidate(
        RhinoObject Object,
        Curve Geometry,
        string PanelId,
        string Cid,
        string CurveCode,
        string AssignedExtrusions,
        IReadOnlyDictionary<string, string> AssignedExtrusionValues,
        string LayerPath);
}
