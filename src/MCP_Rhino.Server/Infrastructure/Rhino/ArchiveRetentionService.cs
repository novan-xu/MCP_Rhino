using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class ArchiveRetentionService : IArchiveRetentionService
{
    public OperationResponse<ArchiveCleanupResponse> Cleanup(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<ArchiveCleanupResponse>.Fail("错误：filePath 不能为空。");
        }

        string sourceDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return OperationResponse<ArchiveCleanupResponse>.Fail($"错误：无法解析文件目录 {filePath}");
        }

        string archiveDirectory = Path.Combine(sourceDirectory, "archive");
        if (!Directory.Exists(archiveDirectory))
        {
            return OperationResponse<ArchiveCleanupResponse>.Ok(new ArchiveCleanupResponse
            {
                SourceFilePath = filePath,
                ArchiveDirectoryPath = archiveDirectory
            }, "archive 目录不存在，无需清理。");
        }

        string sourceFileName = Path.GetFileName(filePath);
        string todayKey = DateTime.Now.ToString("yyMMdd");

        var candidateFiles = new DirectoryInfo(archiveDirectory)
            .GetFiles()
            .Where(file => TryParseArchiveFile(file.Name, sourceFileName, out _))
            .ToList();

        var deletedFiles = new List<string>();
        var retainedFiles = new List<string>();

        foreach (IGrouping<string, FileInfo> group in candidateFiles.GroupBy(file => ExtractDateKey(file.Name)))
        {
            if (string.Equals(group.Key, todayKey, StringComparison.OrdinalIgnoreCase))
            {
                retainedFiles.AddRange(group.Select(file => file.FullName));
                continue;
            }

            FileInfo keepFile = group
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .First();

            retainedFiles.Add(keepFile.FullName);

            foreach (FileInfo file in group.Where(file => !string.Equals(file.FullName, keepFile.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                file.Delete();
                deletedFiles.Add(file.FullName);
            }
        }

        return OperationResponse<ArchiveCleanupResponse>.Ok(new ArchiveCleanupResponse
        {
            SourceFilePath = filePath,
            ArchiveDirectoryPath = archiveDirectory,
            DeletedFileCount = deletedFiles.Count,
            DeletedFiles = deletedFiles,
            RetainedFiles = retainedFiles
        }, deletedFiles.Count == 0 ? "archive 清理完成，无需删除历史文件。" : $"archive 清理完成，删除 {deletedFiles.Count} 个旧备份。");
    }

    private static bool TryParseArchiveFile(string fileName, string sourceFileName, out string dateKey)
    {
        dateKey = string.Empty;
        if (fileName.Length <= 12 + sourceFileName.Length)
        {
            return false;
        }

        if (fileName[6] != '_' || fileName[11] != '_')
        {
            return false;
        }

        if (!fileName[..6].All(char.IsDigit) || !fileName.Substring(7, 4).All(char.IsDigit))
        {
            return false;
        }

        if (!string.Equals(fileName[12..], sourceFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        dateKey = fileName[..6];
        return true;
    }

    private static string ExtractDateKey(string fileName)
    {
        return fileName[..6];
    }
}