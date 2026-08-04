using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryMassResult
{
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public GeometryMassKind Kind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool? IsClosed { get; set; }
    public double? Length { get; set; }
    public double? Area { get; set; }
    public double? Volume { get; set; }
    public GeometryPointData? Centroid { get; set; }
}
