using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingViewDefinition
{
    public string Name { get; set; } = string.Empty;
    public DrawingViewKind Kind { get; set; }
    public double DirectionX { get; set; }
    public double DirectionY { get; set; }
    public double DirectionZ { get; set; }
    public double UpX { get; set; }
    public double UpY { get; set; }
    public double UpZ { get; set; }
}
