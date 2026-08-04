namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryMetadataSnapshot
{
    public int LayerIndex { get; set; }
    public GeometryColorData ObjectColor { get; set; } = new();
    public int ColorSource { get; set; }
    public GeometryColorData PlotColor { get; set; } = new();
    public int PlotColorSource { get; set; }
    public double PlotWeight { get; set; }
    public int PlotWeightSource { get; set; }
    public int LinetypeIndex { get; set; }
    public int LinetypeSource { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public IReadOnlyDictionary<string, string> UserStrings { get; set; } = new Dictionary<string, string>();
}
