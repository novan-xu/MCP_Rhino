using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SetupDrawingViewsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public IReadOnlyList<Guid> ObjectIds { get; set; } = Array.Empty<Guid>();
    public DrawingViewPreset Preset { get; set; } = DrawingViewPreset.Standard8;
    public double? FitMarginPercent { get; set; }
}
