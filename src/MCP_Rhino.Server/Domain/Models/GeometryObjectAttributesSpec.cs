namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryObjectAttributesSpec
{
    public string LayerFullPath { get; set; } = string.Empty;
    public RhinoDisplayColor? Color { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
