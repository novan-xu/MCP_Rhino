using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Interfaces;

public interface ILivePanelCladdingRepository
{
    OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId);

    OperationResponse<PanelCladdingMatchPanelSnapshot> ReadMatchPanel(string filePath, Guid objectId);

    OperationResponse<PanelAttributeCommitResult> CommitAttributes(
        PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit);

    OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath);
}

public interface ILivePanelCladdingSpawnService
{
    OperationResponse<PanelCladdingSpawnResult> Spawn(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        PanelCladdingObjectScope scope);
}

public interface ILivePanelCladdingMatchService
{
    OperationResponse<PanelCladdingMatchResult> Match(
        string filePath,
        Guid sourceObjectId,
        IReadOnlyList<Guid> targetObjectIds);
}

public interface ILivePanelCladdingClearService
{
    OperationResponse<PanelCladdingClearResult> Clear(
        string filePath,
        IReadOnlyList<Guid> objectIds);
}

public interface IPanelCladdingMatchPlanningService
{
    OperationResponse<PanelCladdingMatchPlan> CreatePlan(
        PanelCladdingMatchPanelSnapshot source,
        IReadOnlyList<PanelCladdingMatchPanelSnapshot> targets);
}

public interface ILivePanelCladdingCreateService
{
    OperationResponse<PanelCladdingCreateResult> Create(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        IReadOnlyList<Guid> guideCurveObjectIds);
}

public interface ILivePanelCladdingCurveTemplateService
{
    OperationResponse<PanelCladdingCurveTemplateResult> Apply(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingCurveTemplatePriority priority);
}

public interface ILivePanelCladdingSurfaceSyncRepository
{
    OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        PanelCladdingObjectScope scope);

    OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
        PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook,
        IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount,
        int matchedCurveCount);
}

public interface IPanelCladdingSurfaceSyncService
{
    OperationResponse<PanelCladdingSurfaceSyncResult> Sync(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        string workbookPath,
        bool allowCreateWorkbook,
        PanelCladdingObjectScope scope);
}

public interface IPreparedPanelCladdingWorkbookUpdate : IDisposable
{
    PanelCladdingWorkbookCommitResult Result { get; }
    OperationResponse Commit();
}

public interface IPreparedPanelCladdingWorkbookBatchUpdate : IDisposable
{
    IReadOnlyList<PanelCladdingWorkbookBatchItemResult> Results { get; }
    IReadOnlyList<string> RemovedTypeCodes { get; }
    OperationResponse Commit();
}

public interface IPreparedPanelCladdingMaterialCatalogUpdate : IDisposable
{
    PanelCladdingMaterialCatalogSaveResult Result { get; }
    OperationResponse Commit();
}

public interface IPreparedPanelFrameExtrusionCatalogUpdate : IDisposable
{
    PanelFrameExtrusionCatalogSaveResult Result { get; }
    OperationResponse Commit();
}

public interface IPanelFrameExtrusionScheduleImporter
{
    OperationResponse<PanelFrameExtrusionScheduleImportResult> Import(
        PanelFrameExtrusionScheduleImportRequest request);
}

public interface IPanelCladdingWorkbookRepository
{
    OperationResponse<PanelCladdingMaterialCatalog> ReadMaterialCatalog(string workbookPath) =>
        OperationResponse<PanelCladdingMaterialCatalog>.Fail("Material catalog operations are not available.");

    OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate> PrepareMaterialCatalog(
        PanelCladdingMaterialCatalogSaveRequest request) =>
        OperationResponse<IPreparedPanelCladdingMaterialCatalogUpdate>.Fail(
            "Material catalog operations are not available.");

    OperationResponse<PanelFrameExtrusionCatalog> ReadFrameExtrusionCatalog(string workbookPath) =>
        OperationResponse<PanelFrameExtrusionCatalog>.Fail(
            "Frame extrusion catalog operations are not available.");

    OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate> PrepareFrameExtrusionCatalog(
        PanelFrameExtrusionCatalogSaveRequest request) =>
        OperationResponse<IPreparedPanelFrameExtrusionCatalogUpdate>.Fail(
            "Frame extrusion catalog operations are not available.");

    OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request);

    OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
        PanelCladdingWorkbookBatchUpsert request);
}

public interface IPanelPreviewRenderer
{
    PanelProjectedScene Project(PanelPreviewGeometry preview);
    OperationResponse<byte[]> RenderPng(PanelCladdingLayout layout, int width, int height);
}

