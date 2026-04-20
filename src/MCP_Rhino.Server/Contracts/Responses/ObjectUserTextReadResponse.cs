namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ObjectUserTextReadResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedObjectCount { get; set; }
    public int FoundObjectCount { get; set; }
    public int MissingObjectCount { get; set; }
    public int TotalEntryCount { get; set; }
    public IReadOnlyList<ObjectUserTextRecordResponse> Records { get; set; } = Array.Empty<ObjectUserTextRecordResponse>();
}
