namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ObjectUserTextRecordResponse
{
    public Guid ObjectId { get; set; }
    public string LayerFullPath { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ObjectUserTextEntryResponse> Entries { get; set; } = Array.Empty<ObjectUserTextEntryResponse>();
}
