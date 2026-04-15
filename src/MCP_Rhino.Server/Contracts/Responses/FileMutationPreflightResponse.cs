namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class FileMutationPreflightResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int MatchedObjectCount { get; set; }
    public FileMutationReadinessResponse? Readiness { get; set; }
    public ArchiveSnapshotResponse? Snapshot { get; set; }
    public ArchiveCleanupResponse? Cleanup { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}