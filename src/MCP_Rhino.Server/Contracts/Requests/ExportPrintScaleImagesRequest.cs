using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ExportPrintScaleImagesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public IReadOnlyList<PrintScaleImageViewRequest> Views { get; set; } = Array.Empty<PrintScaleImageViewRequest>();
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public double? DotsPerInch { get; set; }
    public PrintScaleImageMode ScaleMode { get; set; } = PrintScaleImageMode.FitAll;
    public double? ModelScale { get; set; }
    public double? FitScaleMultiplier { get; set; }
    public double? MarginMm { get; set; }
    public bool BackgroundTransparent { get; set; }
    public ObjectColorRequest? SolidBackgroundColor { get; set; }
    public bool OverwriteExisting { get; set; }
}

public sealed class PrintScaleImageViewRequest
{
    public string ViewName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
}
