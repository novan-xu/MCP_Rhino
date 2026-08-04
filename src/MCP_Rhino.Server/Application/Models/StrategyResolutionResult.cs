using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Models;

public sealed class StrategyResolutionResult
{
    public GeometryEditStrategyKind Strategy { get; set; }
    public StrategyResolutionTrace Trace { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
