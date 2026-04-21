namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerModificationPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public IReadOnlyList<LayerModificationImpactResponse> Impacts { get; set; } = Array.Empty<LayerModificationImpactResponse>();
}
