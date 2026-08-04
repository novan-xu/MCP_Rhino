using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReferenceImageModelingTrace
{
    public string ReferenceImageLabel { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int IterationIndex { get; set; }
    public int MassingObjectCount { get; set; }
    public int DetailObjectCount { get; set; }
    public int MaterialAssignmentCount { get; set; }
    public List<string> CompletedStages { get; set; } = new();
    public List<string> KnownGaps { get; set; } = new();
}

public sealed class ReferenceImageIterationDecision
{
    public ReferenceImageIterationDecisionKind Kind { get; set; }
    public double Confidence { get; set; }
    public string NextStage { get; set; } = string.Empty;
    public string StopReason { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
}
