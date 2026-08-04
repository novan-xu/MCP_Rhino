namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetCurrentLayerInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
}
