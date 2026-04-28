using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ApplyEditSurfaceGeometryRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public SurfaceEditSpec EditSpec { get; set; } = new();
    public GeometryEditStrategyKind? ExpectedStrategy { get; set; }
}
