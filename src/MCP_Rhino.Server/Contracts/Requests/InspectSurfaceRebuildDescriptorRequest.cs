namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class InspectSurfaceRebuildDescriptorRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ConfirmedObjectIds { get; set; } = new();
    public Guid? ReferenceCurveObjectId { get; set; }
    public int? ReferenceEdgeIndex { get; set; }
}
