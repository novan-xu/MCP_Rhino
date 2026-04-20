namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DocumentUserStringReadResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public IReadOnlyList<DocumentUserStringEntryResponse> Entries { get; set; } = Array.Empty<DocumentUserStringEntryResponse>();
}
