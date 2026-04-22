using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryAngleResult
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryAngleMeasurementMode Mode { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? AngleRadians { get; set; }
    public double? AngleDegrees { get; set; }
    public GeometryPointData? Vertex { get; set; }
    public GeometryVectorData? FirstDirection { get; set; }
    public GeometryVectorData? SecondDirection { get; set; }
}
