using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class ArchiveSnapshotService : IArchiveSnapshotService
{
    public OperationResponse<ArchiveSnapshotResponse> CreateSnapshot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<ArchiveSnapshotResponse>.Fail("错误：filePath 不能为空。");
        }

        if (!File.Exists(filePath))
        {
            return OperationResponse<ArchiveSnapshotResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        string sourceDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return OperationResponse<ArchiveSnapshotResponse>.Fail($"错误：无法解析文件目录 {filePath}");
        }

        string archiveDirectory = Path.Combine(sourceDirectory, "archive");
        bool archiveDirectoryCreated = false;
        if (!Directory.Exists(archiveDirectory))
        {
            Directory.CreateDirectory(archiveDirectory);
            archiveDirectoryCreated = true;
        }

        string fileName = Path.GetFileName(filePath);
        string archiveFileName = $"{DateTime.Now:yyMMdd_HHmm}_{fileName}";
        string archiveFilePath = Path.Combine(archiveDirectory, archiveFileName);
        bool overwroteExistingSnapshot = File.Exists(archiveFilePath);

        File.Copy(filePath, archiveFilePath, overwrite: true);

        return OperationResponse<ArchiveSnapshotResponse>.Ok(new ArchiveSnapshotResponse
        {
            SourceFilePath = filePath,
            ArchiveDirectoryPath = archiveDirectory,
            ArchiveFilePath = archiveFilePath,
            ArchiveDirectoryCreated = archiveDirectoryCreated,
            OverwroteExistingSnapshot = overwroteExistingSnapshot
        }, "archive 备份创建成功。");
    }
}