using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfaceDirectionTweakApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceDirectionTweakApplyItem> Results { get; set; } = Array.Empty<SurfaceDirectionTweakApplyItem>();
}

public sealed class SurfaceDirectionTweakApplyItem
{
    public Guid OriginalObjectId { get; set; }
    public Guid ObjectId { get; set; }
    public string GeometryKind { get; set; } = string.Empty;
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceDirectionTweakKind> Operations { get; set; } = Array.Empty<SurfaceDirectionTweakKind>();
    public SurfaceDirectionSnapshot? Before { get; set; }
    public SurfaceDirectionSnapshot? After { get; set; }
    public IReadOnlyList<string> MetadataDropped { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}
