namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PreviewDeleteLayersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> FullPaths { get; set; } = new();
}
