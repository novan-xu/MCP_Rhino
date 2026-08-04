namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DocumentUserStringMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<DocumentUserStringMutationResultResponse> Results { get; set; } = Array.Empty<DocumentUserStringMutationResultResponse>();
}
