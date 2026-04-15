namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ArchiveSnapshotResponse
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string ArchiveDirectoryPath { get; set; } = string.Empty;
    public string ArchiveFilePath { get; set; } = string.Empty;
    public bool ArchiveDirectoryCreated { get; set; }
    public bool OverwroteExistingSnapshot { get; set; }
}