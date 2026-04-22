namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryMetricsResult
{
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? Length { get; set; }
    public double? Area { get; set; }
    public double? Perimeter { get; set; }
    public double? Volume { get; set; }
    public bool? IsClosed { get; set; }
}
