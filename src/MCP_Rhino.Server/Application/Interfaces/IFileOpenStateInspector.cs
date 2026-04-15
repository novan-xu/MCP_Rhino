using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IFileOpenStateInspector
{
    OperationResponse<FileMutationReadinessResponse> Inspect(string filePath);
}