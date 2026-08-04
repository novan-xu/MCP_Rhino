using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DrawingExportStateResponse
{
    public DrawingExportResponseStatus Status { get; set; } = DrawingExportResponseStatus.Completed;
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<RhinoLayerCandidate> CandidateLayers { get; set; } = Array.Empty<RhinoLayerCandidate>();
    public string SnapshotId { get; set; } = string.Empty;
    public bool BackgroundStateCaptured { get; set; }
    public int TouchedObjectCount { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
