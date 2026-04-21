namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class LayerModificationImpactResponse
{
    public string TargetFullPath { get; set; } = string.Empty;
    public string? ResolvedNewFullPath { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> FieldDiffs { get; set; } = Array.Empty<string>();
    public IReadOnlyList<LayerSubtreeEntry> AffectedSubLayers { get; set; } = Array.Empty<LayerSubtreeEntry>();
}
