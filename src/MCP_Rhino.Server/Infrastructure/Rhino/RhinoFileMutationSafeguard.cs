using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoFileMutationSafeguard : IFileMutationSafeguard
{
    private readonly IFileOpenStateInspector _fileOpenStateInspector;
    private readonly IArchiveSnapshotService _archiveSnapshotService;
    private readonly IArchiveRetentionService _archiveRetentionService;

    public RhinoFileMutationSafeguard(
        IFileOpenStateInspector fileOpenStateInspector,
        IArchiveSnapshotService archiveSnapshotService,
        IArchiveRetentionService archiveRetentionService)
    {
        _fileOpenStateInspector = fileOpenStateInspector;
        _archiveSnapshotService = archiveSnapshotService;
        _archiveRetentionService = archiveRetentionService;
    }

    public OperationResponse<FileMutationPreflightResponse> BeforeOverwrite(string filePath, int matchedObjectCount)
    {
        OperationResponse<FileMutationReadinessResponse> readinessResult = _fileOpenStateInspector.Inspect(filePath);
        if (!readinessResult.Success || readinessResult.Data is null)
        {
            return OperationResponse<FileMutationPreflightResponse>.Fail(readinessResult.Message);
        }

        if (!readinessResult.Data.IsReady)
        {
            return OperationResponse<FileMutationPreflightResponse>.Fail(readinessResult.Data.Message);
        }

        OperationResponse<ArchiveSnapshotResponse> snapshotResult = _archiveSnapshotService.CreateSnapshot(filePath);
        if (!snapshotResult.Success || snapshotResult.Data is null)
        {
            return OperationResponse<FileMutationPreflightResponse>.Fail(snapshotResult.Message);
        }

        OperationResponse<ArchiveCleanupResponse> cleanupResult = _archiveRetentionService.Cleanup(filePath);
        if (!cleanupResult.Success || cleanupResult.Data is null)
        {
            return OperationResponse<FileMutationPreflightResponse>.Fail(cleanupResult.Message);
        }

        var warnings = new List<string>();
        warnings.AddRange(readinessResult.Data.Signals);

        if (snapshotResult.Data.ArchiveDirectoryCreated)
        {
            warnings.Add($"已创建 archive 目录: {snapshotResult.Data.ArchiveDirectoryPath}");
        }

        warnings.Add($"已创建备份: {snapshotResult.Data.ArchiveFilePath}");

        if (cleanupResult.Data.DeletedFileCount > 0)
        {
            warnings.Add($"已清理 {cleanupResult.Data.DeletedFileCount} 个旧 archive 文件。");
        }

        return OperationResponse<FileMutationPreflightResponse>.Ok(new FileMutationPreflightResponse
        {
            FilePath = filePath,
            MatchedObjectCount = matchedObjectCount,
            Readiness = readinessResult.Data,
            Snapshot = snapshotResult.Data,
            Cleanup = cleanupResult.Data,
            Warnings = warnings
        }, "文件修改前置检查完成。");
    }

    public void AfterOverwrite(string filePath, bool success)
    {
    }
}