namespace MCP_Rhino.Server.Contracts.Requests;

public class LotVisualizationScopeRequest
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<Guid> ObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<string> LotNumberKeys { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> IneffectiveLotValues { get; set; } = Array.Empty<string>();
}

public sealed class PreviewLotVisualizationExportRequest : LotVisualizationScopeRequest
{
    public string? OutputDirectory { get; set; }
    public string? OutputFilePrefix { get; set; }
}

public sealed class ExportLotVisualizationRequest : LotVisualizationScopeRequest
{
    public string? OutputDirectory { get; set; }
    public string? OutputFilePrefix { get; set; }
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public double? DotsPerInch { get; set; }
    public double? FitScaleMultiplier { get; set; }
    public double? MarginMm { get; set; }
    public double? ViewFitMarginPercent { get; set; }
    public bool OverwriteExisting { get; set; }
}
