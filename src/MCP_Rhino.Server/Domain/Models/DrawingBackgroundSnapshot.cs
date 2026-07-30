namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingBackgroundSnapshot
{
    public int RenderBackgroundStyle { get; set; }
    public GeometryColorData RenderBackgroundTop { get; set; } = new();
    public GeometryColorData RenderBackgroundBottom { get; set; } = new();
}
