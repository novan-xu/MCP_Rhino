using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ApplyEditCurveGeometryRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public CurveEditSpec EditSpec { get; set; } = new();
    public GeometryEditStrategyKind? ExpectedStrategy { get; set; }
}
