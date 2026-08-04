namespace MCP_Rhino.Server.Domain.Models;

public sealed class StrategyResolutionTrace
{
    public IReadOnlyList<StrategyResolutionStep> Steps { get; set; } = Array.Empty<StrategyResolutionStep>();
}

public sealed class StrategyResolutionStep
{
    public string RuleName { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public string Reason { get; set; } = string.Empty;
}
