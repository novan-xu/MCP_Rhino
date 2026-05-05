namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingBackgroundSnapshot
{
    public GeometryColorData ViewportBackgroundColor { get; set; } = new();
    public GeometryColorData RenderBackgroundTop { get; set; } = new();
    public GeometryColorData RenderBackgroundBottom { get; set; } = new();
}
