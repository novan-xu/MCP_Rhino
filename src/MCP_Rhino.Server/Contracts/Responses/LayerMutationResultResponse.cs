namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerMutationResultResponse
{
    public string RequestedFullPath { get; set; } = string.Empty;
    public string? ResolvedFullPath { get; set; }
    public bool Success { get; set; }

    // Success=true with Changed=false means the request was accepted but produced
    // no real mutation (e.g. Modify entry whose fields already match the target).
    // Callers that drive downstream work (save prompts, viewport refresh badges)
    // should gate on Changed, not Success.
    public bool Changed { get; set; }

    public string Message { get; set; } = string.Empty;
}
