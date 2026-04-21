namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerDeletionImpactResponse
{
    public string TargetFullPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DirectObjectCount { get; set; }
    public int DescendantObjectCount { get; set; }
    public int TotalAffectedObjectCount { get; set; }
    public bool CurrentLayerInSubtree { get; set; }
    public IReadOnlyList<LayerSubtreeEntry> AffectedSubLayers { get; set; } = Array.Empty<LayerSubtreeEntry>();
}
