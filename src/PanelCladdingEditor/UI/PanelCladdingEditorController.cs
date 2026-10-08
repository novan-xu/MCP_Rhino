using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Application.Services.PanelCladding;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.UI;

public sealed class PanelCladdingEditorController
{
    private readonly ILivePanelCladdingRepository _liveRepository;
    private readonly PanelCladdingSaveService _saveService;
    private readonly IPanelPreviewRenderer _previewRenderer;
    private readonly IPanelCladdingWorkbookRepository? _workbookRepository;
    private readonly IPanelFrameExtrusionScheduleImporter? _frameExtrusionImporter;

    public PanelCladdingEditorController(
        ILivePanelCladdingRepository liveRepository,
        PanelCladdingSaveService saveService,
        IPanelPreviewRenderer previewRenderer,
        PanelCladdingTypeSignatureService signatureService,
        IPanelCladdingWorkbookRepository? workbookRepository = null,
        IPanelFrameExtrusionScheduleImporter? frameExtrusionImporter = null)
    {
        _liveRepository = liveRepository;
        _saveService = saveService;
        _previewRenderer = previewRenderer;
        _ = signatureService; // Retain constructor compatibility while type generation is suspended.
        _workbookRepository = workbookRepository;
        _frameExtrusionImporter = frameExtrusionImporter;
    }

    public OperationResponse<PanelCladdingLayout> Load(string filePath, Guid objectId) =>
        _liveRepository.ReadLayout(filePath, objectId);

    public PanelProjectedScene Project(PanelCladdingLayout layout) => _previewRenderer.Project(layout.Preview);

    public OperationResponse<PanelCladdingSaveResult> Save(PanelCladdingSaveRequest request) =>
        _saveService.Save(request);

    public OperationResponse<PanelCladdingMaterialCatalog> LoadMaterialCatalog(string workbookPath) =>
        _workbookRepository is null
            ? OperationResponse<PanelCladdingMaterialCatalog>.Fail("Material catalog operations are not available.")
            : _workbookRepository.ReadMaterialCatalog(workbookPath);

    public OperationResponse<PanelCladdingMaterialCatalogSaveResult> SaveMaterialCatalog(
        PanelCladdingMaterialCatalogSaveRequest request)
    {
        if (_workbookRepository is null)
        {
            return OperationResponse<PanelCladdingMaterialCatalogSaveResult>.Fail(
                "Material catalog operations are not available.");
        }
        OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate> prepared =
            _workbookRepository.PrepareMaterialCatalog(request);
        if (!prepared.Success || prepared.Data is null)
        {
            return OperationResponse<PanelCladdingMaterialCatalogSaveResult>.Fail(prepared.Message);
        }
        using IPreparedPanelCladdingMaterialCatalogUpdate update = prepared.Data;
        OperationResponse committed = update.Commit();
        return committed.Success
            ? OperationResponse<PanelCladdingMaterialCatalogSaveResult>.Ok(update.Result, committed.Message)
            : OperationResponse<PanelCladdingMaterialCatalogSaveResult>.Fail(committed.Message);
    }

    public OperationResponse<PanelFrameExtrusionCatalog> LoadFrameExtrusionCatalog(string workbookPath) =>
        _workbookRepository is null
            ? OperationResponse<PanelFrameExtrusionCatalog>.Fail(
                "Frame extrusion catalog operations are not available.")
            : _workbookRepository.ReadFrameExtrusionCatalog(workbookPath);

    public OperationResponse<PanelFrameExtrusionCatalogSaveResult> SaveFrameExtrusionCatalog(
        PanelFrameExtrusionCatalogSaveRequest request)
    {
        if (_workbookRepository is null)
        {
            return OperationResponse<PanelFrameExtrusionCatalogSaveResult>.Fail(
                "Frame extrusion catalog operations are not available.");
        }
        OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate> prepared =
            _workbookRepository.PrepareFrameExtrusionCatalog(request);
        if (!prepared.Success || prepared.Data is null)
        {
            return OperationResponse<PanelFrameExtrusionCatalogSaveResult>.Fail(prepared.Message);
        }
        using IPreparedPanelFrameExtrusionCatalogUpdate update = prepared.Data;
        OperationResponse committed = update.Commit();
        return committed.Success
            ? OperationResponse<PanelFrameExtrusionCatalogSaveResult>.Ok(update.Result, committed.Message)
            : OperationResponse<PanelFrameExtrusionCatalogSaveResult>.Fail(committed.Message);
    }

    public OperationResponse<PanelFrameExtrusionScheduleImportResult> ImportFrameExtrusionSchedule(
        PanelFrameExtrusionScheduleImportRequest request) =>
        _frameExtrusionImporter is null
            ? OperationResponse<PanelFrameExtrusionScheduleImportResult>.Fail(
                "PDF extrusion schedule import is not available.")
            : _frameExtrusionImporter.Import(request);

    public OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath) =>
        _liveRepository.SetWorkbookPath(filePath, workbookPath);
}

