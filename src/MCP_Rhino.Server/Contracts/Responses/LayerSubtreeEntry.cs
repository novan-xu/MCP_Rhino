namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerSubtreeEntry
{
    public string FullPath { get; set; } = string.Empty;
    public int DirectObjectCount { get; set; }
}
