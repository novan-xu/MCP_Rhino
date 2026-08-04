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

    public PanelCladdingEditorController(
        ILivePanelCladdingRepository liveRepository,
        PanelCladdingSaveService saveService,
        IPanelPreviewRenderer previewRenderer)
    {
        _liveRepository = liveRepository;
        _saveService = saveService;
        _previewRenderer = previewRenderer;
    }

    public OperationResponse<PanelCladdingLayout> Load(string filePath, Guid objectId) =>
        _liveRepository.ReadLayout(filePath, objectId);

    public PanelProjectedScene Project(PanelCladdingLayout layout) => _previewRenderer.Project(layout.Preview);

    public OperationResponse<PanelCladdingSaveResult> Save(PanelCladdingSaveRequest request) =>
        _saveService.Save(request);
}

