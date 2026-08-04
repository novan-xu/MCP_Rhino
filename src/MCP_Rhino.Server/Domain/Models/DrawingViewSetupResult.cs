using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class DrawingViewSetupResult
{
    public IReadOnlyList<string> ResolvedLayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CreatedViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UpdatedViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ViewNames { get; set; } = Array.Empty<string>();
    public int TargetObjectCount { get; set; }
    public IReadOnlyList<Guid> TargetObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
