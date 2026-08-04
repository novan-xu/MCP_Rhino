namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetObjectMetricsInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<Guid> ObjectIds { get; set; } = new();
}
