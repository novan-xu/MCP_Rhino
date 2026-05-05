using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ExportDrawingPackageRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> LayerQueries { get; set; } = new();
    public List<string> ConfirmedLayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<UserAttributeConditionRequest> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;
    public ObjectColorRequest? TemporaryObjectColor { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public bool ExportPdf { get; set; } = true;
    public bool ExportJpg { get; set; } = true;
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public PageSizeMmRequest? PageSizeMm { get; set; }
    public double? DotsPerInch { get; set; }
    public bool OverwriteExisting { get; set; } = true;
    public List<string> ViewNames { get; set; } = new();
    public DrawingViewPreset Preset { get; set; } = DrawingViewPreset.Standard8;
    public double? FitMarginPercent { get; set; }
}
