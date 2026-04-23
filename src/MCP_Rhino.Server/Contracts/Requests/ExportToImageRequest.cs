namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ExportToImageRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? ViewName { get; set; }
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public double? DotsPerInch { get; set; }
    public bool BackgroundTransparent { get; set; }
    public bool OverwriteExisting { get; set; } = true;
}
