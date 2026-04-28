using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryFrameResult
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public GeometryFrameKind Kind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public GeometryPointData? Origin { get; set; }
    public GeometryVectorData? Tangent { get; set; }
    public GeometryVectorData? Normal { get; set; }
    public GeometryVectorData? XAxis { get; set; }
    public GeometryVectorData? YAxis { get; set; }
    public GeometryVectorData? ZAxis { get; set; }
    public IReadOnlyList<FrameSample> Samples { get; set; } = Array.Empty<FrameSample>();
}
