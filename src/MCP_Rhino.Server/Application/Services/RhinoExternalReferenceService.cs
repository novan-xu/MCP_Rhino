using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoExternalReferenceService
{
    private readonly ILiveRhinoDocumentAccessor _documentAccessor;
    private readonly ILiveExternalReferenceManager _referenceManager;

    public RhinoExternalReferenceService(
        ILiveRhinoDocumentAccessor documentAccessor,
        ILiveExternalReferenceManager referenceManager)
    {
        _documentAccessor = documentAccessor;
        _referenceManager = referenceManager;
    }

    public OperationResponse<WorksessionAttachmentResponse> ListWorksessionAttachments(ListWorksessionAttachmentsRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            OperationResponse<IReadOnlyList<WorksessionAttachmentResult>> list = _referenceManager.ListWorksession(document);
            if (!list.Success || list.Data is null)
            {
                return OperationResponse<WorksessionAttachmentResponse>.Fail(list.Message);
            }

            var response = new WorksessionAttachmentResponse
            {
                FilePath = request.FilePath,
                AttachedFiles = list.Data.Select(item => item.SourceFilePath).ToList(),
                Warnings = Array.Empty<ObjectEditWarning>(),
                Results = list.Data
            };

            return OperationResponse<WorksessionAttachmentResponse>.Ok(response, "Worksession attachments listed from live document.");
        });
    }

    public OperationResponse<LinkedBlockMutationResponse> UpdateLinkedBlock(UpdateLinkedBlockRequest request)
    {
        List<string> definitionNames = NormalizeDefinitionNames(request.DefinitionNames);
        if (definitionNames.Count == 0)
        {
            return OperationResponse<LinkedBlockMutationResponse>.Fail("At least one linked block definition name is required.");
        }

        return _documentAccessor.ExecuteWithUndo(request.FilePath, "MCP: UpdateLinkedBlock", document =>
        {
            OperationResponse<IReadOnlyList<LinkedBlockResult>> updated = _referenceManager.UpdateLinkedBlock(document, definitionNames);
            if (!updated.Success || updated.Data is null)
            {
                return OperationResponse<(bool Mutated, LinkedBlockMutationResponse Result)>.Fail(updated.Message);
            }

            if (updated.Data.Any(item => item.Updated))
            {
                document.Views.Redraw();
            }

            var response = new LinkedBlockMutationResponse
            {
                FilePath = request.FilePath,
                RequestedCount = definitionNames.Count,
                SucceededCount = updated.Data.Count(item => item.Success),
                FailedCount = updated.Data.Count(item => !item.Success),
                Warnings = Array.Empty<ObjectEditWarning>(),
                Results = updated.Data.Select(item => new LinkedBlockResultResponse
                {
                    DefinitionId = item.DefinitionId,
                    DefinitionIndex = item.DefinitionIndex,
                    DefinitionName = item.DefinitionName,
                    InstanceObjectId = item.InstanceObjectId,
                    SourceFilePath = item.SourceFilePath,
                    Success = item.Success,
                    Updated = item.Updated,
                    WasAlreadyDefined = item.WasAlreadyDefined,
                    Message = item.Message
                }).ToList()
            };

            return OperationResponse<(bool Mutated, LinkedBlockMutationResponse Result)>.Ok(
                (updated.Data.Any(item => item.Updated), response),
                "Linked block update completed.");
        });
    }

    private static List<string> NormalizeDefinitionNames(IEnumerable<string>? definitionNames)
    {
        return definitionNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }
}
