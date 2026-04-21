namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerDeletionPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public IReadOnlyList<LayerDeletionImpactResponse> Impacts { get; set; } = Array.Empty<LayerDeletionImpactResponse>();
}
