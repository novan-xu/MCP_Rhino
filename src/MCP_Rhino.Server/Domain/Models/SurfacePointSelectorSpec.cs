using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfacePointSelectorSpec
{
    public SurfacePointSelectorKind Kind { get; set; } = SurfacePointSelectorKind.All;
    public List<int> Indices { get; set; } = new();
    public int? UStart { get; set; }
    public int? UEnd { get; set; }
    public int? VStart { get; set; }
    public int? VEnd { get; set; }
}
