namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SetDrawingExportBackgroundRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public ObjectColorRequest? Color { get; set; }
}
