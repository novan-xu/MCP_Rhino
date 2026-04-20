using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private static string FormatFileMutationReadiness(FileMutationReadinessResponse response)
    {
        var lines = new List<string>
        {
            "# File Mutation Readiness",
            $"- 文件: {response.FilePath}",
            $"- 可修改: {response.IsReady}",
            $"- 已锁定: {response.IsLocked}",
            $"- 检测到 .rhl: {response.LockFileDetected}",
            $"- 说明: {response.Message}"
        };

        if (response.Signals.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Signals:");
            lines.AddRange(response.Signals.Select(signal => $"- {signal}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatArchiveSnapshot(ArchiveSnapshotResponse response)
    {
        return string.Join(Environment.NewLine,
            "# Archive Snapshot",
            $"- 源文件: {response.SourceFilePath}",
            $"- archive目录: {response.ArchiveDirectoryPath}",
            $"- 备份文件: {response.ArchiveFilePath}",
            $"- 新建archive目录: {response.ArchiveDirectoryCreated}",
            $"- 覆盖同名备份: {response.OverwroteExistingSnapshot}");
    }

    private static string FormatArchiveCleanup(ArchiveCleanupResponse response)
    {
        var lines = new List<string>
        {
            "# Archive Cleanup",
            $"- 源文件: {response.SourceFilePath}",
            $"- archive目录: {response.ArchiveDirectoryPath}",
            $"- 删除数量: {response.DeletedFileCount}"
        };

        if (response.DeletedFiles.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Deleted:");
            lines.AddRange(response.DeletedFiles.Select(file => $"- {file}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
