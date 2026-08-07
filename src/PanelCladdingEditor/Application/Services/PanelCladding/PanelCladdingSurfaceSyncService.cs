using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSurfaceSyncService : IPanelCladdingSurfaceSyncService
{
    private readonly ILivePanelCladdingSurfaceSyncRepository _liveRepository;
    private readonly IPanelCladdingWorkbookRepository _workbookRepository;
    private readonly IPanelPreviewRenderer _previewRenderer;
    private readonly PanelCladdingTypeSignatureService _signatureService;
    private readonly PanelCladdingSurfaceSyncPlanningService _planning;

    public PanelCladdingSurfaceSyncService(
        ILivePanelCladdingSurfaceSyncRepository liveRepository,
        IPanelCladdingWorkbookRepository workbookRepository,
        IPanelPreviewRenderer previewRenderer,
        PanelCladdingTypeSignatureService signatureService,
        PanelCladdingSurfaceSyncPlanningService planning)
    {
        _liveRepository = liveRepository;
        _workbookRepository = workbookRepository;
        _previewRenderer = previewRenderer;
        _signatureService = signatureService;
        _planning = planning;
    }

    public OperationResponse<PanelCladdingSurfaceSyncResult> Sync(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        string workbookPath,
        bool allowCreateWorkbook)
    {
        OperationResponse<PanelCladdingSurfaceSyncSnapshot> read =
            _liveRepository.Read(filePath, panelObjectIds);
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
            .Where(surface => surface.CladdingKeyChanged)
            .ToArray();

        if (changedPanels.Length == 0)
        {
            return _liveRepository.Commit(
                new PanelCladdingSurfaceSyncCommitRequest
                {
                    FilePath = filePath,
                    SelectedPanelIds = plan.Panels.Select(panel => panel.ObjectId).ToArray(),
                    SurfaceWrites = changedSurfaces,
                    PanelWrites = Array.Empty<PanelCladdingSurfaceSyncPanelWrite>()
                },
                () => OperationResponse.Ok("No changed panel types to export."),
                Array.Empty<PanelCladdingSurfaceSyncTypeResult>(),
                plan.Surfaces.Count);
        }

        var upserts = new List<PanelCladdingWorkbookUpsert>(changedPanels.Length);
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

            OperationResponse<byte[]> preview = _previewRenderer.RenderPng(panel.Layout, 900, 620);
            if (!preview.Success || preview.Data is null)
            {
                return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                    $"PANEL_CLADDING_SURFACE_SYNC_PREVIEW_FAILED: {panel.ObjectId:D}: {preview.Message}");
            }

            upserts.Add(new PanelCladdingWorkbookUpsert
            {
                WorkbookPath = workbookPath,
                Layout = panel.Layout,
                Identity = identity.Data,
                PreviewPng = preview.Data,
                AllowCreate = allowCreateWorkbook
            });
        }

        OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> preparedResponse =
            _workbookRepository.PrepareBatchUpsert(new PanelCladdingWorkbookBatchUpsert
            {
                WorkbookPath = workbookPath,
                AllowCreate = allowCreateWorkbook,
                Items = upserts
            });
        if (!preparedResponse.Success || preparedResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(preparedResponse.Message);
        }

        using IPreparedPanelCladdingWorkbookBatchUpdate prepared = preparedResponse.Data;
        string finalWorkbookPath = prepared.Results[0].Result.WorkbookPath;
        var resultsByPanel = prepared.Results.ToDictionary(item => item.ObjectId);
        if (resultsByPanel.Count != changedPanels.Length ||
            changedPanels.Any(panel => !resultsByPanel.ContainsKey(panel.ObjectId)))
        {
            return OperationResponse<PanelCladdingSurfaceSyncResult>.Fail(
                "PANEL_CLADDING_SURFACE_SYNC_WORKBOOK_RESULT_MISMATCH");
        }

        var panelWrites = new List<PanelCladdingSurfaceSyncPanelWrite>(changedPanels.Length);
        var types = new List<PanelCladdingSurfaceSyncTypeResult>(changedPanels.Length);
        foreach (PanelCladdingSurfaceSyncPanelPlan panel in changedPanels)
        {
            PanelCladdingWorkbookCommitResult workbookResult = resultsByPanel[panel.ObjectId].Result;
            PanelCladdingTypeIdentity identity = workbookResult.Identity;
            panelWrites.Add(new PanelCladdingSurfaceSyncPanelWrite
            {
                ObjectId = panel.ObjectId,
                ExpectedGeometryFingerprint = panel.Layout.GeometryFingerprint,
                CellValues = identity.NormalizedCellValues,
                TypeCode = identity.TypeCode,
                StoredSignature = identity.StoredSignature
            });
            types.Add(new PanelCladdingSurfaceSyncTypeResult
            {
                PanelObjectId = panel.ObjectId,
                TypeCode = identity.TypeCode,
                StoredSignature = identity.StoredSignature,
                SheetName = workbookResult.SheetName,
                ReusedExistingType = workbookResult.ReusedExistingType
            });
        }

        return _liveRepository.Commit(
            new PanelCladdingSurfaceSyncCommitRequest
            {
                FilePath = filePath,
                WorkbookPath = finalWorkbookPath,
                SelectedPanelIds = plan.Panels.Select(panel => panel.ObjectId).ToArray(),
                SurfaceWrites = changedSurfaces,
                PanelWrites = panelWrites
            },
            prepared.Commit,
            types,
            plan.Surfaces.Count);
    }
}
