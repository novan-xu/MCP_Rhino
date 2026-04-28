namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceLocalCoordinateSystem
{
    public GeometryPointData Origin { get; set; } = new();
    public GeometryVectorData XAxis { get; set; } = new();
    public GeometryVectorData YAxis { get; set; } = new();
    public GeometryVectorData Normal { get; set; } = new();
    public bool IsDegenerate { get; set; }
}
