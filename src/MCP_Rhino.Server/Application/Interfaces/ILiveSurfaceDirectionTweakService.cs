using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveSurfaceDirectionTweakService
{
    OperationResponse<IReadOnlyList<SurfaceDirectionTweakPreviewItem>> Preview(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        IReadOnlyList<SurfaceDirectionTweakKind> operations);

    OperationResponse<IReadOnlyList<SurfaceDirectionTweakApplyItem>> Apply(
        string filePath,
        IReadOnlyList<Guid> objectIds,
        IReadOnlyList<SurfaceDirectionTweakKind> operations,
        string undoRecordName);
}
