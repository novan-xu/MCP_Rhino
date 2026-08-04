using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoViewportCaptureService
{
    private readonly ILiveRhinoViewportCapture _viewportCapture;

    public RhinoViewportCaptureService(ILiveRhinoViewportCapture viewportCapture)
    {
        _viewportCapture = viewportCapture;
    }

    public OperationResponse<ViewportCaptureResponse> Capture(CaptureViewportImageRequest request)
    {
        return _viewportCapture.Capture(request);
    }
}
