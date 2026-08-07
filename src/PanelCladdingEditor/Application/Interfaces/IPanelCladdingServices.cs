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
    OperationResponse<PanelCladdingSpawnResult> Spawn(string filePath, IReadOnlyList<Guid> objectIds);
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

public interface ILivePanelCladdingSurfaceSyncRepository
{
    OperationResponse<PanelCladdingSurfaceSyncSnapshot> Read(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds);

    OperationResponse<PanelCladdingSurfaceSyncResult> Commit(
        PanelCladdingSurfaceSyncCommitRequest request,
        Func<OperationResponse> finalizeWorkbook,
        IReadOnlyList<PanelCladdingSurfaceSyncTypeResult> types,
        int matchedSurfaceCount);
}

public interface IPanelCladdingSurfaceSyncService
{
    OperationResponse<PanelCladdingSurfaceSyncResult> Sync(
        string filePath,
        IReadOnlyList<Guid> panelObjectIds,
        string workbookPath,
        bool allowCreateWorkbook);
}

public interface IPreparedPanelCladdingWorkbookUpdate : IDisposable
{
    PanelCladdingWorkbookCommitResult Result { get; }
    OperationResponse Commit();
}

public interface IPreparedPanelCladdingWorkbookBatchUpdate : IDisposable
{
    IReadOnlyList<PanelCladdingWorkbookBatchItemResult> Results { get; }
    OperationResponse Commit();
}

public interface IPanelCladdingWorkbookRepository
{
    OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request);

    OperationResponse<IPreparedPanelCladdingWorkbookBatchUpdate> PrepareBatchUpsert(
        PanelCladdingWorkbookBatchUpsert request);
}

public interface IPanelPreviewRenderer
{
    PanelProjectedScene Project(PanelPreviewGeometry preview);
    OperationResponse<byte[]> RenderPng(PanelCladdingLayout layout, int width, int height);
}

