using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IFileMutationSafeguard
{
    OperationResponse<FileMutationPreflightResponse> BeforeOverwrite(string filePath, int matchedObjectCount);
    void AfterOverwrite(string filePath, bool success);
}