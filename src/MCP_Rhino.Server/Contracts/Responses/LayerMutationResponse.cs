namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }

    // SucceededCount includes entries that succeeded with no actual change (e.g. Modify
    // no-op where every field already matches). ChangedCount is the subset that actually
    // mutated the document; clients driving save prompts / viewport refresh should look
    // at ChangedCount. NoopCount == SucceededCount - ChangedCount.
    public int SucceededCount { get; set; }
    public int ChangedCount { get; set; }
    public int NoopCount { get; set; }
    public int FailedCount { get; set; }

    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public IReadOnlyList<LayerMutationResultResponse> Results { get; set; } = Array.Empty<LayerMutationResultResponse>();
}
