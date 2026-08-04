namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ObjectEditExecutionResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string CriteriaSummary { get; set; } = string.Empty;
    public int MatchedObjectCount { get; set; }
    public int UpdatedObjectCount { get; set; }
    public int FailedObjectCount { get; set; }
    public int OperationCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<ObjectEditOperationResult> ObjectResults { get; set; } = Array.Empty<ObjectEditOperationResult>();
}