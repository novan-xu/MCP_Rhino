using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Viewport;

[McpServerToolType]
public sealed class CaptureViewportImageTool
{
    private readonly RhinoViewportCaptureService _service;

    public CaptureViewportImageTool(RhinoViewportCaptureService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Capture the current active Rhino viewport or a named open viewport as base64 PNG without writing files, changing zoom, or changing projection.")]
    public OperationResponse<ViewportCaptureResponse> CaptureViewportImage(
        string filePath,
        string? viewName = null,
        ImageSizePxRequest? imageSizePx = null,
        bool backgroundTransparent = false)
    {
        return _service.Capture(new CaptureViewportImageRequest
        {
            FilePath = filePath,
            ViewName = viewName,
            ImageSizePx = imageSizePx,
            BackgroundTransparent = backgroundTransparent
        });
    }
}
