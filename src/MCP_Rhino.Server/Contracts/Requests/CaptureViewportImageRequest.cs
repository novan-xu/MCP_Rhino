namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CaptureViewportImageRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string? ViewName { get; set; }
    public ImageSizePxRequest? ImageSizePx { get; set; }
    public bool BackgroundTransparent { get; set; }
}
