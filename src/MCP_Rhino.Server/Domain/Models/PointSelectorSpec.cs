using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class PointSelectorSpec
{
    public PointSelectorKind Kind { get; set; } = PointSelectorKind.All;
    public List<int> Indices { get; set; } = new();
    public int? Start { get; set; }
    public int? End { get; set; }
}
