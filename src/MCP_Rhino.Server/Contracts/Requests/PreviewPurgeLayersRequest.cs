namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PreviewPurgeLayersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> FullPaths { get; set; } = new();
}
