using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfaceFrontBackFlipApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceFrontBackFlipApplyItem> Results { get; set; } = Array.Empty<SurfaceFrontBackFlipApplyItem>();
}

public sealed class SurfaceFrontBackFlipApplyItem
{
    public Guid OriginalObjectId { get; set; }
    public Guid ObjectId { get; set; }
    public string GeometryKind { get; set; } = string.Empty;
    public string UndoRecordName { get; set; } = string.Empty;
    public SurfaceDirectionSnapshot? Before { get; set; }
    public SurfaceDirectionSnapshot? After { get; set; }
    public IReadOnlyList<string> MetadataDropped { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}
