namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoObjectFilterResult
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalObjectCount { get; set; }
    public int MatchedCount { get; set; }
    public string CriteriaSummary { get; set; } = string.Empty;
    public IReadOnlyList<RhinoObjectInfo> Objects { get; set; } = Array.Empty<RhinoObjectInfo>();
}