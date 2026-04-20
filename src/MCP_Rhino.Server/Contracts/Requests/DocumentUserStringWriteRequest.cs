namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class DocumentUserStringWriteRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<DocumentUserStringEntryRequest> Entries { get; set; } = new();
}
