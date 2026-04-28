namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceControlPointGridSnapshot
{
    public string SurfaceKind { get; set; } = string.Empty;
    public int CountU { get; set; }
    public int CountV { get; set; }
    public bool IsValid { get; set; }
    public GeometryBoundingBoxData BoundingBox { get; set; } = new();
    public IReadOnlyList<GeometryPointData> SampleCorners { get; set; } = Array.Empty<GeometryPointData>();
}
