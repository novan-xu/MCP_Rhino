using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IArchiveRetentionService
{
    OperationResponse<ArchiveCleanupResponse> Cleanup(string filePath);
}