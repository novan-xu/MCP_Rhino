using PanelCladdingEditor.Application.Interfaces;
using PanelCladdingEditor.Contracts.Responses;
using PanelCladdingEditor.Domain.Models.PanelCladding;

namespace PanelCladdingEditor.Application.Services.PanelCladding;

public sealed class PanelCladdingSaveService
{
    private readonly ILivePanelCladdingRepository _liveRepository;
    private readonly IPanelCladdingWorkbookRepository _workbookRepository;
    private readonly IPanelPreviewRenderer _previewRenderer;
    private readonly PanelCladdingTypeSignatureService _signatureService;

    public PanelCladdingSaveService(
        ILivePanelCladdingRepository liveRepository,
        IPanelCladdingWorkbookRepository workbookRepository,
        IPanelPreviewRenderer previewRenderer,
        PanelCladdingTypeSignatureService signatureService)
    {
        _liveRepository = liveRepository;
        _workbookRepository = workbookRepository;
        _previewRenderer = previewRenderer;
        _signatureService = signatureService;
    }

    public OperationResponse<PanelCladdingSaveResult> Save(PanelCladdingSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || request.ObjectId == Guid.Empty)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail("PANEL_CLADDING_TARGET_REQUIRED");
        }

        OperationResponse<PanelCladdingLayout> read = _liveRepository.ReadLayout(request.FilePath, request.ObjectId);
        if (!read.Success || read.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(read.Message);
        }

        PanelCladdingLayout layout = read.Data;
        if (!string.Equals(layout.GeometryFingerprint, request.ExpectedGeometryFingerprint, StringComparison.Ordinal))
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(
                "PANEL_CLADDING_STALE_EDITOR: panel geometry or attributes changed; reload before saving.");
        }

        OperationResponse<PanelCladdingTypeIdentity> identityResponse = _signatureService.Create(
            layout,
            request.CellValues,
            request.SystemCode);
        if (!identityResponse.Success || identityResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(identityResponse.Message);
        }

        OperationResponse<byte[]> preview = _previewRenderer.RenderPng(layout, 900, 620);
        if (!preview.Success || preview.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(preview.Message);
        }

        var upsert = new PanelCladdingWorkbookUpsert
        {
            WorkbookPath = request.WorkbookPath,
            Layout = layout,
            Identity = identityResponse.Data,
            PreviewPng = preview.Data,
            AllowCreate = request.AllowCreateWorkbook
        };
        OperationResponse<IPreparedPanelCladdingWorkbookUpdate> preparedResponse = _workbookRepository.PrepareUpsert(upsert);
        if (!preparedResponse.Success || preparedResponse.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(preparedResponse.Message);
        }

        using IPreparedPanelCladdingWorkbookUpdate prepared = preparedResponse.Data;
        PanelCladdingTypeIdentity finalIdentity = prepared.Result.Identity;
        var writes = new Dictionary<string, string>(finalIdentity.NormalizedCellValues, StringComparer.OrdinalIgnoreCase)
        {
            [PanelCladdingKeyService.TypeCodeKey] = finalIdentity.TypeCode,
            [PanelCladdingKeyService.SignatureKey] = finalIdentity.StoredSignature
        };

        OperationResponse<PanelAttributeCommitResult> committed = _liveRepository.CommitAttributes(
            new PanelAttributeCommitRequest
            {
                FilePath = request.FilePath,
                ObjectId = request.ObjectId,
                ExpectedGeometryFingerprint = request.ExpectedGeometryFingerprint,
                UserTextDeletes = new[]
                {
                    PanelCladdingKeyService.LegacyTypeCodeKey,
                    PanelCladdingKeyService.LegacySignatureKey
                },
                UserTextWrites = writes,
                WorkbookPath = request.WorkbookPath
            },
            prepared.Commit);
        if (!committed.Success || committed.Data is null)
        {
            return OperationResponse<PanelCladdingSaveResult>.Fail(committed.Message);
        }

        return OperationResponse<PanelCladdingSaveResult>.Ok(new PanelCladdingSaveResult
        {
            ObjectId = request.ObjectId,
            TypeCode = finalIdentity.TypeCode,
            StoredSignature = finalIdentity.StoredSignature,
            WorkbookPath = prepared.Result.WorkbookPath,
            SheetName = prepared.Result.SheetName,
            ReusedExistingType = prepared.Result.ReusedExistingType
        }, prepared.Result.ReusedExistingType
            ? "Existing panel cladding type reused."
            : "New panel cladding type created.");
    }
}

