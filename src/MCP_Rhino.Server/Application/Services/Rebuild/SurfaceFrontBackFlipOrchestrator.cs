using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services.Rebuild;

public sealed class SurfaceFrontBackFlipOrchestrator : ISurfaceFrontBackFlipOrchestrator
{
    internal const string UndoRecordName = "MCP:FlipSurfaceFrontBack";

    private readonly ILiveSurfaceFrontBackFlipService _liveService;

    public SurfaceFrontBackFlipOrchestrator(ILiveSurfaceFrontBackFlipService liveService)
    {
        _liveService = liveService;
    }

    public OperationResponse<SurfaceFrontBackFlipApplyResponse> Apply(ApplySurfaceFrontBackFlipRequest request)
    {
        OperationResponse validation = ValidateIds(request.ConfirmedObjectIds);
        if (!validation.Success)
        {
            return OperationResponse<SurfaceFrontBackFlipApplyResponse>.Fail(validation.Message);
        }

        OperationResponse<IReadOnlyList<SurfaceFrontBackFlipApplyItem>> apply = _liveService.Apply(
            request.FilePath,
            request.ConfirmedObjectIds,
            UndoRecordName);
        if (!apply.Success || apply.Data is null)
        {
            return OperationResponse<SurfaceFrontBackFlipApplyResponse>.Fail(apply.Message);
        }

        return OperationResponse<SurfaceFrontBackFlipApplyResponse>.Ok(new SurfaceFrontBackFlipApplyResponse
        {
            FilePath = request.FilePath,
            UndoRecordName = UndoRecordName,
            Results = apply.Data
        }, "Surface front/back flip applied.");
    }

    private static OperationResponse ValidateIds(IReadOnlyList<Guid> objectIds)
    {
        if (objectIds.Count == 0 || objectIds.Any(id => id == Guid.Empty))
        {
            return OperationResponse.Fail("ConfirmedObjectIds must contain at least one non-empty ObjectId.");
        }

        return OperationResponse.Ok();
    }
}
