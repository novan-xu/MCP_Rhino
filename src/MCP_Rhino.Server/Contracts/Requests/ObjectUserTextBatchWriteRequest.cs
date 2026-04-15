namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectUserTextBatchWriteRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ObjectScopedUserTextEntryRequest> Entries { get; set; } = new();
}