namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingExportSnapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public uint DocumentRuntimeSerialNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DrawingBackgroundSnapshot Background { get; set; } = new();
    public IReadOnlyDictionary<Guid, GeometryMetadataSnapshot> ObjectSnapshots { get; set; } = new Dictionary<Guid, GeometryMetadataSnapshot>();
}
