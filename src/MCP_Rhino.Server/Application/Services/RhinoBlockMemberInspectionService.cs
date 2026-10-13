using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoBlockMemberInspectionService
{
    private readonly ILiveBlockMemberInspector _inspector;
    public RhinoBlockMemberInspectionService(ILiveBlockMemberInspector inspector) => _inspector = inspector;

    public OperationResponse<BlockMemberInspectionResponse> Inspect(BlockMemberInspectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return OperationResponse<BlockMemberInspectionResponse>.Fail("FilePath is required.");
        if (request.ObjectIds is null || request.ObjectIds.Count is < 1 or > 1000 || request.ObjectIds.Contains(Guid.Empty))
            return OperationResponse<BlockMemberInspectionResponse>.Fail("Supply 1 to 1000 non-empty top-level block instance IDs.");
        if (request.MaxLeaves is < 1 or > 50000)
            return OperationResponse<BlockMemberInspectionResponse>.Fail("MaxLeaves must be between 1 and 50000.");
        return _inspector.Inspect(new BlockMemberInspectionRequest
        {
            FilePath = request.FilePath, ObjectIds = request.ObjectIds.Distinct().ToList(), MaxLeaves = request.MaxLeaves
        });
    }
}
