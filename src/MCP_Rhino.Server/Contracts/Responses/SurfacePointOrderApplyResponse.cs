namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class SurfacePointOrderApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<SurfacePointOrderApplyItem> Results { get; set; } = Array.Empty<SurfacePointOrderApplyItem>();
}

public sealed class SurfacePointOrderApplyItem
{
    public Guid OriginalObjectId { get; set; }
    public Guid ObjectId { get; set; }
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<string> MetadataDropped { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
}
