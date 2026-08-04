namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoLayerCandidate
{
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int ObjectCount { get; set; }
}