using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.File;

public sealed class FileMutationPreflightSkill
{
    private readonly IFileMutationSafeguard _fileMutationSafeguard;

    public FileMutationPreflightSkill(IFileMutationSafeguard fileMutationSafeguard)
    {
        _fileMutationSafeguard = fileMutationSafeguard;
    }

    public OperationResponse<FileMutationPreflightResponse> Prepare(string filePath, int matchedObjectCount = 0)
    {
        return _fileMutationSafeguard.BeforeOverwrite(filePath, matchedObjectCount);
    }
}