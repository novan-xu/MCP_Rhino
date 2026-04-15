namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class FilterObjectsByLayerRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
}