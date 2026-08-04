namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ObjectUserTextDeleteRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ObjectScopedUserTextKeyRequest> Entries { get; set; } = new();
}
