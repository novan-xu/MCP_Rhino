using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveSurfaceFrontBackFlipService
{
    OperationResponse<IReadOnlyList<SurfaceFrontBackFlipApplyItem>> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        string undoRecordName);
}
