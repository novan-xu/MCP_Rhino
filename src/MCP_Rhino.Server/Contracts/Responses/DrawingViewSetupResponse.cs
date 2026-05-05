using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DrawingViewSetupResponse
{
    public DrawingExportResponseStatus Status { get; set; } = DrawingExportResponseStatus.Completed;
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<RhinoLayerCandidate> CandidateLayers { get; set; } = Array.Empty<RhinoLayerCandidate>();
    public IReadOnlyList<string> ResolvedLayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> CreatedViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> UpdatedViewNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ViewNames { get; set; } = Array.Empty<string>();
    public int TargetObjectCount { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
