using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class StandardFourPointSurfaceRebuildResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string RebuildUndoRecordName { get; set; } = string.Empty;
    public string FrontBackFlipUndoRecordName { get; set; } = string.Empty;
    public string DirectionUndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceDirectionTweakKind> PostRebuildDirectionOperations { get; set; } = Array.Empty<SurfaceDirectionTweakKind>();
    public IReadOnlyList<StandardFourPointSurfaceRebuildItem> Results { get; set; } = Array.Empty<StandardFourPointSurfaceRebuildItem>();
}

public sealed class StandardFourPointSurfaceRebuildItem
{
    public Guid OriginalObjectId { get; set; }
    public Guid ObjectId { get; set; }
    public bool RebuildSkipped { get; set; }
    public string RebuildSkipReason { get; set; } = string.Empty;
    public bool FrontBackFlipSkipped { get; set; }
    public string FrontBackFlipSkipReason { get; set; } = string.Empty;
    public bool DirectionSkipped { get; set; }
    public string DirectionSkipReason { get; set; } = string.Empty;
    public IReadOnlyList<string> MetadataDropped { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
