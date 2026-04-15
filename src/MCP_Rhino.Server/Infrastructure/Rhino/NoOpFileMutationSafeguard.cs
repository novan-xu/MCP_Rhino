using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class NoOpFileMutationSafeguard : IFileMutationSafeguard
{
    public OperationResponse<FileMutationPreflightResponse> BeforeOverwrite(string filePath, int matchedObjectCount)
    {
        return OperationResponse<FileMutationPreflightResponse>.Ok(new FileMutationPreflightResponse
        {
            FilePath = filePath,
            MatchedObjectCount = matchedObjectCount
        });
    }

    public void AfterOverwrite(string filePath, bool success)
    {
    }
}