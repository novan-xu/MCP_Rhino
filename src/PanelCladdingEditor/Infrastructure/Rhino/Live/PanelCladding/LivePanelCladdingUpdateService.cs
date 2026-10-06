extern alias rhinocommon;

using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;
using Brep = rhinocommon::Rhino.Geometry.Brep;
using Curve = rhinocommon::Rhino.Geometry.Curve;
using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;
using ObjectAttributes = rhinocommon::Rhino.DocObjects.ObjectAttributes;
using ObjectColorSource = rhinocommon::Rhino.DocObjects.ObjectColorSource;
using ObjectEnumeratorSettings = rhinocommon::Rhino.DocObjects.ObjectEnumeratorSettings;
using ObjectType = rhinocommon::Rhino.DocObjects.ObjectType;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using RhinoObject = rhinocommon::Rhino.DocObjects.RhinoObject;

namespace PanelCladdingEditor.Infrastructure.Rhino.Live.PanelCladding;

public sealed class LivePanelCladdingUpdateService : ILivePanelCladdingUpdateService
{
    private readonly ILivePanelCladdingRepository _layouts;
    private readonly PanelCladdingKeyService _keys;
    private readonly PanelCladdingSpawnPlanningService _spawnPlanning;
    private readonly PanelCladdingDependencyReconciliationService _reconciliation;

    public LivePanelCladdingUpdateService(
        ILivePanelCladdingRepository layouts,
        PanelCladdingKeyService keys,
        PanelCladdingSpawnPlanningService spawnPlanning,
        PanelCladdingDependencyReconciliationService reconciliation)
    {
        _layouts = layouts;
        _keys = keys;
        _spawnPlanning = spawnPlanning;
        _reconciliation = reconciliation;
    }

    public OperationResponse<PanelCladdingUpdateResult> Update(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds)
    {
        OperationResponse<RhinoDoc> resolved = ResolveDocument(filePath);
        if (!resolved.Success || resolved.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(resolved.Message);
        }
        RhinoDoc document = resolved.Data;
        Guid[] sourcePanelIds = (panelObjectIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (sourcePanelIds.Length == 0)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(
                "PANEL_CLADDING_UPDATE_PANEL_SELECTION_REQUIRED");
        }

        // Validate the source geometry and PID before entering the mutation scope.
        var sources = ReadPanelSources(document, sourcePanelIds);
        if (!sources.Success || sources.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(sources.Message);
        }

        return Apply(document, filePath, sourcePanelIds);
    }

    private OperationResponse<PanelCladdingUpdateResult> UpdateDependencies(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<Guid> sourcePanelIds,
        ref bool documentMutationOccurred)
    {
        // Read again after the CID writes, so selection and generation use the same document state.
        var sources = ReadPanelSources(document, sourcePanelIds);
        if (!sources.Success || sources.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(sources.Message);
        }
        PanelCladdingUpdateSelection selection = PanelCladdingUpdateSelectionService.CreatePlan(sources.Data);

        var generator = new LivePanelCladdingSpawnService(_layouts, _keys, _spawnPlanning);
        OperationResponse<IReadOnlyList<PreparedDependency>> preparedResponse = PrepareDependencies(
            document,
            filePath,
            selection.ProcessableSources,
            generator);
        if (!preparedResponse.Success || preparedResponse.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(preparedResponse.Message);
        }

        IReadOnlyList<PreparedDependency> prepared = preparedResponse.Data;
        try
        {
            var ambiguousDependencies = new List<Guid>();
            IReadOnlyList<PanelCladdingExistingDependency> existing = ReadManagedDependencies(
                document,
                sources.Data,
                selection.ProcessableSources.Select(source => source.ObjectId).ToHashSet(),
                ambiguousDependencies);
            OperationResponse<PanelCladdingDependencyReconciliationPlan> plan =
                _reconciliation.CreatePlan(
                    prepared.Select(item => item.Expected).ToArray(),
                    existing);
            if (!plan.Success || plan.Data is null)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(plan.Message);
            }

            return ApplyDependencies(document, selection, prepared, plan.Data,
                ambiguousDependencies, ref documentMutationOccurred);
        }
        finally
        {
            foreach (PreparedDependency dependency in prepared)
            {
                dependency.Dispose();
            }
        }
    }

    private static OperationResponse<IReadOnlyList<PanelCladdingUpdateSource>> ReadPanelSources(
        RhinoDoc document,
        IReadOnlyList<Guid> panelObjectIds)
    {
        var sources = new List<PanelCladdingUpdateSource>(panelObjectIds.Count);
        foreach (Guid objectId in panelObjectIds)
        {
            RhinoObject? rhinoObject = document.Objects.FindId(objectId);
            if (rhinoObject?.Geometry is not Brep)
            {
                return OperationResponse<IReadOnlyList<PanelCladdingUpdateSource>>.Fail(
                    $"PANEL_CLADDING_UPDATE_PANEL_BREP_NOT_FOUND: {objectId:D}");
            }
            string panelId = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
            if (panelId.Length == 0)
            {
                return OperationResponse<IReadOnlyList<PanelCladdingUpdateSource>>.Fail(
                    $"PANEL_CLADDING_UPDATE_PID_REQUIRED: {objectId:D}");
            }
            sources.Add(new PanelCladdingUpdateSource(objectId, panelId,
                PanelCladdingCidService.ResolvePanelCid(panelId, LivePanelCladdingCidService.Read(rhinoObject.Attributes))));
        }
        return OperationResponse<IReadOnlyList<PanelCladdingUpdateSource>>.Ok(sources);
    }

    private static OperationResponse<IReadOnlyList<PreparedDependency>> PrepareDependencies(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<PanelCladdingUpdateSource> sources,
        LivePanelCladdingSpawnService generator)
    {
        var prepared = new List<PreparedDependency>();
        foreach (PanelCladdingUpdateSource source in sources)
        {
            OperationResponse<LivePanelCladdingSpawnService.PreparedPanel> surfaces =
                generator.PreparePanel(
                    document,
                    filePath,
                    source.ObjectId,
                    PanelCladdingObjectScope.Surfaces,
                    allowEmptySurfacePlan: true);
            if (!surfaces.Success || surfaces.Data is null)
            {
                DisposePrepared(prepared);
                return OperationResponse<IReadOnlyList<PreparedDependency>>.Fail(
                    $"PANEL_CLADDING_UPDATE_SURFACE_PREPARE_FAILED: {source.ObjectId:D}: {surfaces.Message}");
            }

            OperationResponse<LivePanelCladdingSpawnService.PreparedPanel> curves =
                generator.PreparePanel(
                    document,
                    filePath,
                    source.ObjectId,
                    PanelCladdingObjectScope.Curves);
            if (!curves.Success || curves.Data is null)
            {
                foreach (LivePanelCladdingSpawnService.PreparedRegion region in surfaces.Data.Regions)
                {
                    region.Geometry.Dispose();
                }
                DisposePrepared(prepared);
                return OperationResponse<IReadOnlyList<PreparedDependency>>.Fail(
                    $"PANEL_CLADDING_UPDATE_CURVE_PREPARE_FAILED: {source.ObjectId:D}: {curves.Message}");
            }

            prepared.AddRange(surfaces.Data.Regions.Select(region =>
                PreparedDependency.FromSurface(source, region)));
            prepared.AddRange(curves.Data.Curves.Select(curve =>
                PreparedDependency.FromCurve(source, curve)));
        }
        return OperationResponse<IReadOnlyList<PreparedDependency>>.Ok(prepared);
    }

    private static IReadOnlyList<PanelCladdingExistingDependency> ReadManagedDependencies(
        RhinoDoc document,
        IReadOnlyList<PanelCladdingUpdateSource> sources,
        IReadOnlySet<Guid> processableIds,
        List<Guid> ambiguousDependencies)
    {
        var selectedPanels = sources.GroupBy(source => source.PanelId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var sourceIds = sources.Select(source => source.ObjectId).ToHashSet();
        var result = new List<PanelCladdingExistingDependency>();
        foreach (RhinoObject rhinoObject in document.Objects.GetObjectList(
            new ObjectEnumeratorSettings
            {
                NormalObjects = true,
                LockedObjects = true,
                HiddenObjects = true,
                ActiveObjects = true,
                ReferenceObjects = false,
                ObjectTypeFilter = ObjectType.Brep | ObjectType.Curve
            }))
        {
            if (sourceIds.Contains(rhinoObject.Id))
            {
                continue;
            }
            string layerPath = rhinoObject.Attributes.LayerIndex >= 0
                ? document.Layers[rhinoObject.Attributes.LayerIndex]?.FullPath ?? string.Empty
                : string.Empty;
            PanelCladdingDependencyKind? kind = rhinoObject.Geometry switch
            {
                Brep when PanelCladdingSpawnPlanningService.IsManagedSurfaceLayerPath(layerPath) =>
                    PanelCladdingDependencyKind.Surface,
                Curve when PanelCladdingSpawnPlanningService.IsManagedExtrusionLayerPath(layerPath) =>
                    PanelCladdingDependencyKind.Curve,
                _ => null
            };
            if (!kind.HasValue)
            {
                continue;
            }

            string panelId = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.PanelIdUserTextKey).Trim();
            string cid = GetCanonicalUserText(
                rhinoObject.Attributes,
                PanelCladdingSpawnPlanningService.CidUserTextKey).Trim();
            if (!selectedPanels.TryGetValue(panelId, out var candidates) || cid.Length == 0)
            {
                continue;
            }
            var owners = PanelCladdingUpdateSelectionService.FindDependencyOwners(candidates, panelId, cid);
            if (owners.Count != 1 || !processableIds.Contains(owners[0].ObjectId))
            {
                // Legacy unsuffixed dependencies cannot be assigned to one of several selected roles.
                // Including skipped sources here also protects their geometry from another panel's update.
                if (owners.Count > 1 && owners.Any(owner => processableIds.Contains(owner.ObjectId)))
                {
                    ambiguousDependencies.Add(rhinoObject.Id);
                }
                continue;
            }
            result.Add(new PanelCladdingExistingDependency
            {
                ObjectId = rhinoObject.Id,
                PanelId = panelId,
                Cid = cid,
                Kind = kind.Value
            });
        }
        return result;
    }

    private OperationResponse<PanelCladdingUpdateResult> Apply(
        RhinoDoc document,
        string filePath,
        IReadOnlyList<Guid> sourcePanelIds)
    {
        var panelCidChanges = LivePanelCladdingCidService.Prepare(document, sourcePanelIds);
        if (!panelCidChanges.Success || panelCidChanges.Data is null)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(panelCidChanges.Message);
        }
        uint undoRecord = document.CurrentUndoRecordSerialNumber;
        bool ownsUndoRecord = undoRecord == 0U;
        if (ownsUndoRecord)
        {
            undoRecord = document.BeginUndoRecord("Update Panel Cladding Dependencies");
        }
        if (undoRecord == 0U)
        {
            return OperationResponse<PanelCladdingUpdateResult>.Fail(
                "PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE");
        }

        bool documentMutationOccurred = false;
        OperationResponse<PanelCladdingUpdateResult>? outcome = null;
        try
        {
            OperationResponse cidCommit = LivePanelCladdingCidService.Apply(document, panelCidChanges.Data);
            documentMutationOccurred |= panelCidChanges.Data.Any(change => change.Applied);
            outcome = cidCommit.Success
                ? UpdateDependencies(document, filePath, sourcePanelIds, ref documentMutationOccurred)
                : OperationResponse<PanelCladdingUpdateResult>.Fail(cidCommit.Message);
            if (outcome.Success && outcome.Data is not null)
            {
                if (outcome.Data.DuplicateCidGroups.Count > 0)
                {
                    document.Objects.UnselectAll();
                    foreach (Guid id in outcome.Data.DuplicateCidGroups.SelectMany(group => group.PanelObjectIds))
                    {
                        document.Objects.FindId(id)?.Select(true);
                    }
                }
                document.Views.Redraw();
            }
        }
        catch (Exception ex)
        {
            outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(
                $"PANEL_CLADDING_UPDATE_FAILED: {ex.Message}");
        }
        finally
        {
            if (ownsUndoRecord)
            {
                document.EndUndoRecord(undoRecord);
                if (outcome is { Success: false } && documentMutationOccurred && !document.Undo())
                {
                    outcome = OperationResponse<PanelCladdingUpdateResult>.Fail(
                        $"{outcome.Message}; PANEL_CLADDING_UPDATE_ROLLBACK_FAILED");
                }
            }
        }
        return outcome ?? OperationResponse<PanelCladdingUpdateResult>.Fail(
            "PANEL_CLADDING_UPDATE_NO_RESULT");
    }

    private static OperationResponse<PanelCladdingUpdateResult> ApplyDependencies(
        RhinoDoc document,
        PanelCladdingUpdateSelection selection,
        IReadOnlyList<PreparedDependency> prepared,
        PanelCladdingDependencyReconciliationPlan plan,
        IReadOnlyList<Guid> ambiguousDependencies,
        ref bool documentMutationOccurred)
    {
        var preparedByIdentity = prepared.ToDictionary(
            item => IdentityKey(item.Expected), StringComparer.OrdinalIgnoreCase);
        var createdSurfaces = new List<Guid>();
        var createdCurves = new List<Guid>();
        var updatedSurfaces = new List<Guid>();
        var updatedCurves = new List<Guid>();
        var deleted = new List<Guid>();
        foreach (PanelCladdingDependencyUpdateAction action in plan.Updates)
        {
            PreparedDependency dependency = preparedByIdentity[IdentityKey(action.Expected)];
            OperationResponse updated = UpdateExisting(
                document, action.ObjectId, dependency, ref documentMutationOccurred);
            if (!updated.Success)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(updated.Message);
            }
            (dependency.Expected.Kind == PanelCladdingDependencyKind.Surface
                ? updatedSurfaces : updatedCurves).Add(action.ObjectId);
        }
        foreach (PanelCladdingExpectedDependency expected in plan.Creates)
        {
            PreparedDependency dependency = preparedByIdentity[IdentityKey(expected)];
            OperationResponse<Guid> created = CreateMissing(document, dependency, ref documentMutationOccurred);
            if (!created.Success || created.Data == Guid.Empty)
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(created.Message);
            }
            (expected.Kind == PanelCladdingDependencyKind.Surface
                ? createdSurfaces : createdCurves).Add(created.Data);
            documentMutationOccurred = true;
        }
        foreach (Guid objectId in plan.Deletes)
        {
            RhinoObject? existingObject = document.Objects.FindId(objectId);
            if (existingObject is null || !document.Objects.Delete(
                    existingObject,
                    quiet: true,
                    ignoreModes: true))
            {
                return OperationResponse<PanelCladdingUpdateResult>.Fail(
                    $"PANEL_CLADDING_UPDATE_DELETE_FAILED: {objectId:D}");
            }
            deleted.Add(objectId);
            documentMutationOccurred = true;
        }
        return OperationResponse<PanelCladdingUpdateResult>.Ok(new PanelCladdingUpdateResult
        {
            SourcePanelIds = selection.ProcessableSources.Select(source => source.ObjectId).ToArray(),
            DuplicateCidGroups = selection.DuplicateCidGroups,
            PreservedAmbiguousDependencyIds = ambiguousDependencies,
            CreatedSurfaceIds = createdSurfaces,
            CreatedCurveIds = createdCurves,
            UpdatedSurfaceIds = updatedSurfaces,
            UpdatedCurveIds = updatedCurves,
            DeletedObjectIds = deleted
        });
    }

    private static OperationResponse UpdateExisting(
        RhinoDoc document,
        Guid objectId,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        RhinoObject? existingObject = document.Objects.FindId(objectId);
        if (existingObject is null)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_UPDATE_REPLACE_FAILED: {objectId:D}");
        }

        OperationResponse<ObjectAttributes> attributes = CreateAttributes(
            document,
            dependency,
            ref documentMutationOccurred);
        if (!attributes.Success || attributes.Data is null)
        {
            return OperationResponse.Fail(attributes.Message);
        }

        attributes.Data.Mode = existingObject.Attributes.Mode;
        bool replaced = document.Objects.Replace(
            objectId,
            dependency.Geometry,
            ignoreModes: true);
        if (!replaced)
        {
            return OperationResponse.Fail($"PANEL_CLADDING_UPDATE_REPLACE_FAILED: {objectId:D}");
        }
        documentMutationOccurred = true;
        return document.Objects.ModifyAttributes(objectId, attributes.Data, quiet: true)
            ? OperationResponse.Ok()
            : OperationResponse.Fail($"PANEL_CLADDING_UPDATE_ATTRIBUTES_FAILED: {objectId:D}");
    }

    private static OperationResponse<Guid> CreateMissing(
        RhinoDoc document,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        OperationResponse<ObjectAttributes> attributes = CreateAttributes(
            document,
            dependency,
            ref documentMutationOccurred);
        if (!attributes.Success || attributes.Data is null)
        {
            return OperationResponse<Guid>.Fail(attributes.Message);
        }
        Guid objectId = dependency.Geometry switch
        {
            Brep brep => document.Objects.AddBrep(brep, attributes.Data),
            Curve curve => document.Objects.AddCurve(curve, attributes.Data),
            _ => Guid.Empty
        };
        return objectId == Guid.Empty
            ? OperationResponse<Guid>.Fail(
                $"PANEL_CLADDING_UPDATE_CREATE_FAILED: {dependency.Expected.Cid}")
            : OperationResponse<Guid>.Ok(objectId);
    }

    private static OperationResponse<ObjectAttributes> CreateAttributes(
        RhinoDoc document,
        PreparedDependency dependency,
        ref bool documentMutationOccurred)
    {
        string layerPath = dependency.SurfacePlan?.LayerPath ??
            dependency.CurvePlan?.LayerPath ??
            string.Empty;
        int priorLayerIndex = document.Layers.FindByFullPath(layerPath, -1);
        bool layerNeedsMutation = priorLayerIndex < 0;
        if (!layerNeedsMutation && dependency.SurfacePlan is not null)
        {
            var expectedColor = System.Drawing.Color.FromArgb(
                dependency.SurfacePlan.LayerColor.Red,
                dependency.SurfacePlan.LayerColor.Green,
                dependency.SurfacePlan.LayerColor.Blue);
            layerNeedsMutation = document.Layers[priorLayerIndex]?.Color.ToArgb() != expectedColor.ToArgb();
        }

        OperationResponse<int> layer = dependency.SurfacePlan is not null
            ? LivePanelCladdingSpawnService.EnsureMaterialLayer(document, dependency.SurfacePlan)
            : LivePanelCladdingSpawnService.EnsureLayer(
                document,
                layerPath);
        if (layerNeedsMutation && document.Layers.FindByFullPath(layerPath, -1) >= 0)
        {
            documentMutationOccurred = true;
        }
        if (!layer.Success)
        {
            return OperationResponse<ObjectAttributes>.Fail(layer.Message);
        }

        var attributes = new ObjectAttributes
        {
            Name = PanelCladdingCidService.ShortName(dependency.Expected.Cid),
            LayerIndex = layer.Data,
            ColorSource = dependency.CurvePlan is null
                ? ObjectColorSource.ColorFromLayer
                : ObjectColorSource.ColorFromObject
        };
        if (dependency.CurvePlan is not null)
        {
            attributes.ObjectColor = System.Drawing.Color.FromArgb(
                dependency.CurvePlan.ObjectColor.Red,
                dependency.CurvePlan.ObjectColor.Green,
                dependency.CurvePlan.ObjectColor.Blue);
        }
        IReadOnlyDictionary<string, string> writes = dependency.SurfacePlan?.UserTextWrites ??
            dependency.CurvePlan?.UserTextWrites ??
            new Dictionary<string, string>();
        foreach ((string key, string value) in writes)
        {
            attributes.SetUserString(key, value);
        }
        return OperationResponse<ObjectAttributes>.Ok(attributes);
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

    private static string GetCanonicalUserText(ObjectAttributes attributes, string canonicalKey)
    {
        var values = attributes.GetUserStrings();
        foreach (string? key in values?.AllKeys ?? Array.Empty<string?>())
        {
            if (key is not null && string.Equals(key, canonicalKey, StringComparison.OrdinalIgnoreCase))
            {
                return values?[key] ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string IdentityKey(PanelCladdingExpectedDependency item) =>
        $"{item.Kind}|{item.PanelId.Trim()}|{item.Cid.Trim()}";

    private static void DisposePrepared(IEnumerable<PreparedDependency> dependencies)
    {
        foreach (PreparedDependency dependency in dependencies)
        {
            dependency.Dispose();
        }
    }

    private sealed class PreparedDependency : IDisposable
    {
        private PreparedDependency(
            PanelCladdingExpectedDependency expected,
            GeometryBase geometry,
            PanelCladdingSpawnRegionPlan? surfacePlan,
            PanelCladdingExtrusionCurvePlan? curvePlan)
        {
            Expected = expected;
            Geometry = geometry;
            SurfacePlan = surfacePlan;
            CurvePlan = curvePlan;
        }

        public PanelCladdingExpectedDependency Expected { get; }
        public GeometryBase Geometry { get; }
        public PanelCladdingSpawnRegionPlan? SurfacePlan { get; }
        public PanelCladdingExtrusionCurvePlan? CurvePlan { get; }

        public static PreparedDependency FromSurface(
            PanelCladdingUpdateSource source,
            LivePanelCladdingSpawnService.PreparedRegion region) =>
            new(
                new PanelCladdingExpectedDependency
                {
                    SourcePanelObjectId = source.ObjectId,
                    PanelId = source.PanelId,
                    Cid = region.Region.Cid,
                    Kind = PanelCladdingDependencyKind.Surface
                },
                region.Geometry,
                region.Region,
                null);

        public static PreparedDependency FromCurve(
            PanelCladdingUpdateSource source,
            LivePanelCladdingSpawnService.PreparedCurve curve) =>
            new(
                new PanelCladdingExpectedDependency
                {
                    SourcePanelObjectId = source.ObjectId,
                    PanelId = source.PanelId,
                    Cid = curve.CurvePlan.Cid,
                    Kind = PanelCladdingDependencyKind.Curve
                },
                curve.Geometry,
                null,
                curve.CurvePlan);

        public void Dispose() => Geometry.Dispose();
    }
}
