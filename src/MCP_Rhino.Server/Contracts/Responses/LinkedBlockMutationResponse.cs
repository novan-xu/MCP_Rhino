namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LinkedBlockMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<LinkedBlockResultResponse> Results { get; set; } = Array.Empty<LinkedBlockResultResponse>();
}
