namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DocumentUserStringMutationResultResponse
{
    public string? Section { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}
