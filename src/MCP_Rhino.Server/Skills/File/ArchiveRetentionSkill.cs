using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.File;

public sealed class ArchiveRetentionSkill
{
    private readonly IArchiveRetentionService _archiveRetentionService;

    public ArchiveRetentionSkill(IArchiveRetentionService archiveRetentionService)
    {
        _archiveRetentionService = archiveRetentionService;
    }

    public OperationResponse<ArchiveCleanupResponse> Cleanup(string filePath)
    {
        return _archiveRetentionService.Cleanup(filePath);
    }
}