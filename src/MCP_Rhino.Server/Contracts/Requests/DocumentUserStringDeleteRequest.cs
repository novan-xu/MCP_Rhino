namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class DocumentUserStringDeleteRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<DocumentUserStringEntryRequest> Entries { get; set; } = new();
}
