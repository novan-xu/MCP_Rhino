using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IArchiveSnapshotService
{
    OperationResponse<ArchiveSnapshotResponse> CreateSnapshot(string filePath);
}