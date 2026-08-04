namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingLayerScopeResult
{
    public bool NeedsLayerSelection { get; set; }
    public IReadOnlyList<RhinoLayerCandidate> CandidateLayers { get; set; } = Array.Empty<RhinoLayerCandidate>();
    public IReadOnlyList<string> ResolvedLayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<int> ResolvedLayerIndices { get; set; } = Array.Empty<int>();
}
