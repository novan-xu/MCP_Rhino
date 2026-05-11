namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetSelectedObjectsInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
}
