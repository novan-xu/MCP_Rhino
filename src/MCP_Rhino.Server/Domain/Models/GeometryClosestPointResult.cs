using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryClosestPointResult
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryClosestPointTargetKind TargetKind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SourceGeometryTypeName { get; set; } = string.Empty;
    public string TargetGeometryTypeName { get; set; } = string.Empty;
    public GeometryPointData? SourcePoint { get; set; }
    public GeometryPointData? TargetPoint { get; set; }
    public double? Distance { get; set; }
    public double? SourceParameter { get; set; }
    public double? TargetParameter { get; set; }
    public double? SourceU { get; set; }
    public double? SourceV { get; set; }
    public double? TargetU { get; set; }
    public double? TargetV { get; set; }
}
