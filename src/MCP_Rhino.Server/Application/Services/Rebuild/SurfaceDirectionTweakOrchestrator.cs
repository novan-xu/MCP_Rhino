using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Rules;

namespace MCP_Rhino.Server.Application.Services.Rebuild;

public sealed class SurfaceDirectionTweakOrchestrator : ISurfaceDirectionTweakOrchestrator
{
    internal const string UndoRecordName = "MCP:TweakSurfaceDirections";

    private readonly ILiveSurfaceDirectionTweakService _liveService;

    public SurfaceDirectionTweakOrchestrator(ILiveSurfaceDirectionTweakService liveService)
    {
        _liveService = liveService;
    }

    public OperationResponse<SurfaceDirectionTweakPreviewResponse> Preview(PreviewSurfaceDirectionTweakRequest request)
    {
        IReadOnlyList<SurfaceDirectionTweakKind> operations = SurfaceDirectionTweakDefaults.NormalizeOperations(request.Operations);
        OperationResponse validation = Validate(request.ConfirmedObjectIds, operations);
        if (!validation.Success)
        {
            return OperationResponse<SurfaceDirectionTweakPreviewResponse>.Fail(validation.Message);
        }

        OperationResponse<IReadOnlyList<SurfaceDirectionTweakPreviewItem>> preview = _liveService.Preview(
            request.FilePath,
            request.ConfirmedObjectIds,
            operations);
        if (!preview.Success || preview.Data is null)
        {
            return OperationResponse<SurfaceDirectionTweakPreviewResponse>.Fail(preview.Message);
        }

        return OperationResponse<SurfaceDirectionTweakPreviewResponse>.Ok(new SurfaceDirectionTweakPreviewResponse
        {
            FilePath = request.FilePath,
            Results = preview.Data
        }, "Surface direction tweak preview generated.");
    }

    public OperationResponse<SurfaceDirectionTweakApplyResponse> Apply(ApplySurfaceDirectionTweakRequest request)
    {
        IReadOnlyList<SurfaceDirectionTweakKind> operations = SurfaceDirectionTweakDefaults.NormalizeOperations(request.Operations);
        OperationResponse validation = Validate(request.ConfirmedObjectIds, operations);
        if (!validation.Success)
        {
            return OperationResponse<SurfaceDirectionTweakApplyResponse>.Fail(validation.Message);
        }

        OperationResponse<IReadOnlyList<SurfaceDirectionTweakApplyItem>> apply = _liveService.Apply(
            request.FilePath,
            request.ConfirmedObjectIds,
            operations,
            UndoRecordName);
        if (!apply.Success || apply.Data is null)
        {
            return OperationResponse<SurfaceDirectionTweakApplyResponse>.Fail(apply.Message);
        }

        return OperationResponse<SurfaceDirectionTweakApplyResponse>.Ok(new SurfaceDirectionTweakApplyResponse
        {
            FilePath = request.FilePath,
            UndoRecordName = UndoRecordName,
            Results = apply.Data
        }, "Surface direction tweak applied.");
    }

    private static OperationResponse Validate(
        IReadOnlyList<Guid> objectIds,
        IReadOnlyList<SurfaceDirectionTweakKind> operations)
    {
        if (objectIds.Count == 0 || objectIds.Any(id => id == Guid.Empty))
        {
            return OperationResponse.Fail("ConfirmedObjectIds must contain at least one non-empty ObjectId.");
        }

        if (operations.Any(operation => !Enum.IsDefined(operation)))
        {
            return OperationResponse.Fail("Operations contains an unsupported surface direction tweak.");
        }

        return OperationResponse.Ok();
    }
}
