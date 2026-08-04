namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PurgeLayersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> FullPaths { get; set; } = new();
}
