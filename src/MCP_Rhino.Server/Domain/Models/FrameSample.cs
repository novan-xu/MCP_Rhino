namespace MCP_Rhino.Server.Domain.Models;

public sealed class FrameSample
{
    public int Index { get; set; }
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public GeometryPointData? Origin { get; set; }
    public GeometryVectorData? Tangent { get; set; }
    public GeometryVectorData? Normal { get; set; }
    public GeometryVectorData? XAxis { get; set; }
    public GeometryVectorData? YAxis { get; set; }
    public GeometryVectorData? ZAxis { get; set; }
    public bool IsDegenerate { get; set; }
    public string Message { get; set; } = string.Empty;
}
