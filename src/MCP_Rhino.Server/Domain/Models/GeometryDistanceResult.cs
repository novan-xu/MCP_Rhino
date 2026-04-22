namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryDistanceResult
{
    public string EntryId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FromGeometryTypeName { get; set; } = string.Empty;
    public string ToGeometryTypeName { get; set; } = string.Empty;
    public GeometryPointData? FromPoint { get; set; }
    public GeometryPointData? ToPoint { get; set; }
    public double? Distance { get; set; }
}
