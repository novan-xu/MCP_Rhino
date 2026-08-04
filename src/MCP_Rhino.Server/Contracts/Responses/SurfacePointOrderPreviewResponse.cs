using MCP_Rhino.Server.Domain.Models;
using MCP_Rhino.Server.Application.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfacePointOrderPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<SurfacePointOrderPreviewItem> Results { get; set; } = Array.Empty<SurfacePointOrderPreviewItem>();
}

public sealed class SurfacePointOrderPreviewItem
{
    public Guid ObjectId { get; set; }
    public SurfacePointOrderPlan? Plan { get; set; }
    public SurfaceRebuildPreviewSummary? PreviewSummary { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}
