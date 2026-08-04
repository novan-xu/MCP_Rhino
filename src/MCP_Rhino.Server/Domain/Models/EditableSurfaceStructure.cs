namespace MCP_Rhino.Server.Domain.Models;

public sealed class EditableSurfaceStructure
{
    public string SurfaceKind { get; set; } = string.Empty;
    public int DegreeU { get; set; }
    public int DegreeV { get; set; }
    public int CountU { get; set; }
    public int CountV { get; set; }
    public bool IsClosedU { get; set; }
    public bool IsClosedV { get; set; }
    public bool IsPeriodicU { get; set; }
    public bool IsPeriodicV { get; set; }
    public GeometryBoundingBoxData BoundingBox { get; set; } = new();
}

public sealed class GeometryBoundingBoxData
{
    public GeometryPointData Min { get; set; } = new();
    public GeometryPointData Max { get; set; } = new();
}
