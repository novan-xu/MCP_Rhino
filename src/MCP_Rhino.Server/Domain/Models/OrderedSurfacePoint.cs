namespace MCP_Rhino.Server.Domain.Models;

public sealed class OrderedSurfacePoint
{
    public int OriginalIndex { get; set; }
    public double SortKey { get; set; }
    public GeometryPointData Position3d { get; set; } = new();
    public GeometryPoint2dData Position2dInLcs { get; set; } = new();
}

public sealed class GeometryPoint2dData
{
    public double X { get; set; }
    public double Y { get; set; }
}
