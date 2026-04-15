using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Skills.File;

namespace MCP_Rhino.Server.Agents.File;

public sealed class FileArchiveAgent
{
    private readonly FileOpenStateCheckSkill _fileOpenStateCheckSkill;
    private readonly ArchiveSnapshotSkill _archiveSnapshotSkill;
    private readonly ArchiveRetentionSkill _archiveRetentionSkill;
    private readonly FileMutationPreflightSkill _fileMutationPreflightSkill;

    public FileArchiveAgent(
        FileOpenStateCheckSkill fileOpenStateCheckSkill,
        ArchiveSnapshotSkill archiveSnapshotSkill,
        ArchiveRetentionSkill archiveRetentionSkill,
        FileMutationPreflightSkill fileMutationPreflightSkill)
    {
        _fileOpenStateCheckSkill = fileOpenStateCheckSkill;
        _archiveSnapshotSkill = archiveSnapshotSkill;
        _archiveRetentionSkill = archiveRetentionSkill;
        _fileMutationPreflightSkill = fileMutationPreflightSkill;
    }

    public OperationResponse<FileMutationReadinessResponse> InspectReadiness(string filePath)
    {
        return _fileOpenStateCheckSkill.Check(filePath);
    }

    public OperationResponse<ArchiveSnapshotResponse> CreateSnapshot(string filePath)
    {
        return _archiveSnapshotSkill.Create(filePath);
    }

    public OperationResponse<ArchiveCleanupResponse> CleanupArchive(string filePath)
    {
        return _archiveRetentionSkill.Cleanup(filePath);
    }

    public OperationResponse<FileMutationPreflightResponse> PrepareForMutation(string filePath, int matchedObjectCount = 0)
    {
        return _fileMutationPreflightSkill.Prepare(filePath, matchedObjectCount);
    }
}