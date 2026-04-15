using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino;

public sealed class RhinoFileOpenStateInspector : IFileOpenStateInspector
{
    public OperationResponse<FileMutationReadinessResponse> Inspect(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResponse<FileMutationReadinessResponse>.Fail("错误：filePath 不能为空。");
        }

        if (!File.Exists(filePath))
        {
            return OperationResponse<FileMutationReadinessResponse>.Fail($"错误：未找到文件 {filePath}");
        }

        string lockFilePath = $"{filePath}.rhl";
        bool lockFileDetected = File.Exists(lockFilePath);
        var signals = new List<string>();

        if (lockFileDetected)
        {
            signals.Add($"检测到 Rhino 锁文件痕迹: {lockFilePath}");
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            return OperationResponse<FileMutationReadinessResponse>.Ok(new FileMutationReadinessResponse
            {
                FilePath = filePath,
                IsReady = false,
                IsLocked = true,
                LockFileDetected = lockFileDetected,
                Message = "检测到文件可能正在打开或被占用，请先关闭后再执行修改。",
                Signals = signals
            }, "文件当前不可安全修改。");
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResponse<FileMutationReadinessResponse>.Fail($"无法访问文件 {filePath}: {ex.Message}");
        }

        if (lockFileDetected)
        {
            signals.Add("虽然检测到 .rhl 文件，但当前文件句柄可独占访问，按可修改处理。");
        }

        return OperationResponse<FileMutationReadinessResponse>.Ok(new FileMutationReadinessResponse
        {
            FilePath = filePath,
            IsReady = true,
            IsLocked = false,
            LockFileDetected = lockFileDetected,
            Message = "文件可安全修改。",
            Signals = signals
        }, "文件当前可安全修改。");
    }
}