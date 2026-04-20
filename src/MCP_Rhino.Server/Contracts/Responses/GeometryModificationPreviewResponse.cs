namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeometryModificationPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string CriteriaSummary { get; set; } = string.Empty;
    public int MatchedObjectCount { get; set; }
    public int PreviewObjectCount { get; set; }
    public int OperationCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<ObjectEditOperationResult> ObjectResults { get; set; } = Array.Empty<ObjectEditOperationResult>();
}
