using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSurfaceSyncService : IPanelCladdingSurfaceSyncService
{
    private readonly ILivePanelCladdingSurfaceSyncRepository _liveRepository;
    private readonly PanelCladdingTypeSignatureService _signatureService;
    private readonly PanelCladdingSurfaceSyncPlanningService _planning;

    public PanelCladdingSurfaceSyncService(
        ILivePanelCladdingSurfaceSyncRepository liveRepository,
        PanelCladdingTypeSignatureService signatureService,
        PanelCladdingSurfaceSyncPlanningService planning)
    {
        _liveRepository = liveRepository;
        _signatureService = signatureService;
        _planning = planning;
    }

    public PanelCladdingSurfaceSyncService(
        ILivePanelCladdingSurfaceSyncRepository liveRepository,
        IPanelCladdingWorkbookRepository workbookRepository,
        IPanelPreviewRenderer previewRenderer,
        PanelCladdingTypeSignatureService signatureService,
        PanelCladdingSurfaceSyncPlanningService planning)
        : this(liveRepository, signatureService, planning)
    {
        _ = workbookRepository;
        _ = previewRenderer;
    }

    public OperationResponse<PanelCladdingSurfaceSyncResult> Sync(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        string workbookPath,
        bool allowCreateWorkbook,
        PanelCladdingObjectScope scope)
    {
        _ = allowCreateWorkbook;
        OperationResponse<PanelCladdingSurfaceSyncSnapshot> read =
            _liveRepository.Read(filePath, panelObjectIds, scope);
        if (!read.Success || read.Data is null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(read.Message);
        }

        OperationResponse<PanelCladdingSurfaceSyncPlan> planned = _planning.CreatePlan(read.Data);
        if (!planned.Success || planned.Data is null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(planned.Message);
        }
        PanelCladdingSurfaceSyncPlan plan = planned.Data;
        PanelCladdingSurfaceSyncPanelPlan[] changedPanels = plan.Panels
            .Where(panel => panel.CladdingChanged)
            .ToArray();
        PanelCladdingSurfaceSyncSurfacePlan[] changedSurfaces = plan.Surfaces
            .Where(surface => surface.MetadataChanged)
            .ToArray();
        PanelCladdingSurfaceSyncCurvePlan[] changedCurves = plan.Curves
            .Where(curve => curve.MetadataChanged)
            .ToArray();
        Guid[] skippedPanelIds = plan.Issues
            .Select(issue => issue.PanelObjectId)
            .Where(objectId => objectId != Guid.Empty)
            .Distinct()
            .ToArray();

        var identities = new Dictionary<Guid, PanelCladdingTypeIdentity>();
        var persistedCellValues = new Dictionary<Guid, IReadOnlyDictionary<string, string>>();
        foreach (PanelCladdingSurfaceSyncPanelPlan panel in changedPanels)
        {
            OperationResponse<PanelCladdingTypeIdentity> identity = _signatureService.Create(
                panel.Layout,
                panel.CellValues,
                panel.Layout.SystemCode);
            if (!identity.Success || identity.Data is null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_IDENTITY_FAILED: {panel.ObjectId:D}: {identity.Message}");
            }
            identities[panel.ObjectId] = identity.Data;

            OperationResponse<IReadOnlyDictionary<string, string>> persisted =
                BuildPersistedCellValues(panel.Layout, panel.CellValues);
            if (!persisted.Success || persisted.Data is null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_CELL_GRAPH_INVALID: {panel.ObjectId:D}: {persisted.Message}");
            }
            persistedCellValues[panel.ObjectId] = persisted.Data;
        }

        var panelWrites = new List<PanelCladdingSurfaceSyncPanelWrite>(changedPanels.Length);
        var types = new List<PanelCladdingSurfaceSyncTypeResult>(changedPanels.Length);
        foreach (PanelCladdingSurfaceSyncPanelPlan panel in changedPanels)
        {
            PanelCladdingTypeIdentity identity = identities[panel.ObjectId];
            panelWrites.Add(new PanelCladdingSurfaceSyncPanelWrite
            {
                ObjectId = panel.ObjectId,
                ExpectedGeometryFingerprint = panel.Layout.GeometryFingerprint,
                HorizontalOffsets = panel.Layout.HorizontalOffsets,
                VerticalOffsets = panel.Layout.VerticalOffsets,
                Topology = panel.Layout.Topology,
                // The signature uses a normalized graph, but Rhino persistence must retain the
                // exact geometry-derived owner/parent graph produced by PCSyncSrf.
                CellValues = persistedCellValues[panel.ObjectId],
                CladdingLogic = panel.CladdingLogic,
                TypeCode = identity.TypeCode,
                StoredSignature = identity.StoredSignature,
                UnitWidth = PanelCladdingKeyService.FormatUnitDimension(panel.Layout.Width),
                UnitHeight = PanelCladdingKeyService.FormatUnitDimension(panel.Layout.Height),
                UnitDimension = $"{PanelCladdingKeyService.FormatUnitDimension(panel.Layout.Width)}x{PanelCladdingKeyService.FormatUnitDimension(panel.Layout.Height)}"
            });
            types.Add(new PanelCladdingSurfaceSyncTypeResult
            {
                PanelObjectId = panel.ObjectId,
                TypeCode = identity.TypeCode,
                StoredSignature = identity.StoredSignature,
                SheetName = string.Empty,
                ReusedExistingType = false
            });
        }

        string associatedWorkbook = string.IsNullOrWhiteSpace(read.Data.WorkbookPath)
            ? workbookPath
            : read.Data.WorkbookPath;
        return _liveRepository.Commit(
            new PanelCladdingSurfaceSyncCommitRequest
            {
                Scope = plan.Scope,
                FilePath = filePath,
                WorkbookPath = associatedWorkbook,
                SelectedPanelIds = plan.SelectedPanelIds,
                SkippedPanelIds = skippedPanelIds,
                Issues = plan.Issues,
                RemovedWorkbookTypeCodes = Array.Empty<string>(),
                SurfaceWrites = changedSurfaces,
                CurveWrites = changedCurves,
                PanelWrites = panelWrites
            },
            () => OperationResponse.Ok("No workbook type sheets are generated."),
            types,
            plan.Surfaces.Count,
            plan.Curves.Count);
    }

    public static OperationResponse<IReadOnlyDictionary<string, string>> BuildPersistedCellValues(
        PanelCladdingLayout layout,
        IReadOnlyDictionary<string, string> plannedCellValues)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PanelCladdingCell cell in layout.Cells
            .OrderBy(item => item.Column)
            .ThenBy(item => item.Row))
        {
            if (plannedCellValues is null ||
                !plannedCellValues.TryGetValue(cell.UserTextKey, out string? value))
            {
                return OperationResponse<IReadOnlyDictionary<string, string>>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_CELL_VALUE_MISSING: {cell.ShortLabel} ({cell.UserTextKey})");
            }
            result[cell.UserTextKey] = PanelCladdingKeyService.EncodeCellValueForStorage(value);
        }
        return OperationResponse<IReadOnlyDictionary<string, string>>.Ok(result);
    }

    public static OperationResponse ValidateAppliedPanelCellValues(
        IReadOnlyDictionary<string, string> current,
        PanelCladdingSurfaceSyncPanelWrite plannedWrite)
    {
        var actual = new Dictionary<string, string>(
            current ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string expected) in plannedWrite.CellValues ??
            new Dictionary<string, string>())
        {
            if (!actual.TryGetValue(key, out string? value) ||
                !string.Equals(value, expected, StringComparison.Ordinal))
            {
                return OperationResponse.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_CELL_WRITE_MISMATCH: {key}: " +
                    $"expected '{expected}', actual '{value ?? "<missing>"}'");
            }
        }
        if (!actual.TryGetValue(PanelCladdingKeyService.CladdingLogicKey, out string? logic) ||
            !string.Equals(logic, plannedWrite.CladdingLogic, StringComparison.Ordinal))
        {
            return OperationResponse.Fail(
                $"PANEL_CLADDING_SURFACE_SYNC_LOGIC_WRITE_MISMATCH: " +
                $"expected '{plannedWrite.CladdingLogic}', actual '{logic ?? "<missing>"}'");
        }
        return OperationResponse.Ok();
    }
}
