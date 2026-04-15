namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class FileMutationReadinessResponse
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public bool IsLocked { get; set; }
    public bool LockFileDetected { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> Signals { get; set; } = Array.Empty<string>();
}