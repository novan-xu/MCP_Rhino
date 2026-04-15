using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Skills.File;

public sealed class ArchiveSnapshotSkill
{
    private readonly IArchiveSnapshotService _archiveSnapshotService;

    public ArchiveSnapshotSkill(IArchiveSnapshotService archiveSnapshotService)
    {
        _archiveSnapshotService = archiveSnapshotService;
    }

    public OperationResponse<ArchiveSnapshotResponse> Create(string filePath)
    {
        return _archiveSnapshotService.CreateSnapshot(filePath);
    }
}