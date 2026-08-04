using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Interfaces;

public interface ILivePanelCladdingRepository
{
    OperationResponse<PanelCladdingLayout> ReadLayout(string filePath, Guid objectId);

    OperationResponse<PanelAttributeCommitResult> CommitAttributes(
        PanelAttributeCommitRequest request,
        Func<OperationResponse> finalizeExternalCommit);

    OperationResponse<string> SetWorkbookPath(string filePath, string workbookPath);
}

public interface IPreparedPanelCladdingWorkbookUpdate : IDisposable
{
    PanelCladdingWorkbookCommitResult Result { get; }
    OperationResponse Commit();
}

public interface IPanelCladdingWorkbookRepository
{
    OperationResponse<IPreparedPanelCladdingWorkbookUpdate> PrepareUpsert(PanelCladdingWorkbookUpsert request);
}

public interface IPanelPreviewRenderer
{
    PanelProjectedScene Project(PanelPreviewGeometry preview);
    OperationResponse<byte[]> RenderPng(PanelCladdingLayout layout, int width, int height);
}

