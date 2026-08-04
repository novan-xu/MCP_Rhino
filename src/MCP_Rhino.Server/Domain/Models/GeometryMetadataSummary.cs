namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryMetadataSummary
{
    public int LayerIndex { get; set; }
    public string LayerFullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ColorSource { get; set; } = string.Empty;
    public GeometryColorData ObjectColor { get; set; } = new();
    public int UserStringCount { get; set; }
}

public sealed class GeometryColorData
{
    public int A { get; set; }
    public int R { get; set; }
    public int G { get; set; }
    public int B { get; set; }
}
