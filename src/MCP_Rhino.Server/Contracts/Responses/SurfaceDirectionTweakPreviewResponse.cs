using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfaceDirectionTweakPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceDirectionTweakPreviewItem> Results { get; set; } = Array.Empty<SurfaceDirectionTweakPreviewItem>();
}

public sealed class SurfaceDirectionTweakPreviewItem
{
    public Guid ObjectId { get; set; }
    public string GeometryKind { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceDirectionTweakKind> Operations { get; set; } = Array.Empty<SurfaceDirectionTweakKind>();
    public SurfaceDirectionSnapshot? Before { get; set; }
    public SurfaceDirectionSnapshot? After { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}
