using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryClosestPointEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryClosestPointTargetKind TargetKind { get; set; } = GeometryClosestPointTargetKind.Auto;
    public GeometryAnalysisReferenceRequest Source { get; set; } = new();
    public GeometryAnalysisReferenceRequest Target { get; set; } = new();
}
