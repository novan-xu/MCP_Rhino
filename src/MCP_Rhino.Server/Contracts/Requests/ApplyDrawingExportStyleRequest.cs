namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ApplyDrawingExportStyleRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public ObjectColorRequest? ObjectColor { get; set; }
}
