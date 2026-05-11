using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CaptureReferenceImageModelingQaViewsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string? ReferenceImagePath { get; set; }
    public string? ReferenceImageLabel { get; set; }
    public ReferenceImageVisualQaCheckpointKind CheckpointKind { get; set; } = ReferenceImageVisualQaCheckpointKind.InitialMassing;
    public List<Guid> ObjectIds { get; set; } = new();
    public List<string> ViewNames { get; set; } = new();
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public bool BackgroundTransparent { get; set; }
    public int MaxObjectSummaries { get; set; } = 20;
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
}
