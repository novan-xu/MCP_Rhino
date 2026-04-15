namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ArchiveCleanupResponse
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string ArchiveDirectoryPath { get; set; } = string.Empty;
    public int DeletedFileCount { get; set; }
    public IReadOnlyList<string> DeletedFiles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RetainedFiles { get; set; } = Array.Empty<string>();
}