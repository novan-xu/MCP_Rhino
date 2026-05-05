namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class RestoreDrawingExportStateRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
}
