extern alias rhinocommon;

using System.Drawing;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using RhinoView = rhinocommon::Rhino.Display.RhinoView;
using ViewTypeFilter = rhinocommon::Rhino.Display.ViewTypeFilter;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live;

public sealed class LiveRhinoViewportCapture : ILiveRhinoViewportCapture
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;
    private const int MaxWidth = 2048;
    private const int MaxHeight = 2048;

    private readonly ILiveRhinoDocumentAccessor _documentAccessor;

    public LiveRhinoViewportCapture(ILiveRhinoDocumentAccessor documentAccessor)
    {
        _documentAccessor = documentAccessor;
    }

    public OperationResponse<ViewportCaptureResponse> Capture(CaptureViewportImageRequest request)
    {
        return _documentAccessor.Execute(request.FilePath, document =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return OperationResponse<ViewportCaptureResponse>.Fail("Viewport capture requires Windows.");
            }

            OperationResponse<RhinoView> resolvedView = ResolveView(document, request.ViewName);
            if (!resolvedView.Success || resolvedView.Data is null)
            {
                return OperationResponse<ViewportCaptureResponse>.Fail(resolvedView.Message);
            }

            List<ObjectEditWarning> warnings = new();
            int requestedWidth = request.ImageSizePx?.Width ?? DefaultWidth;
            int requestedHeight = request.ImageSizePx?.Height ?? DefaultHeight;
            int width = Clamp(requestedWidth, 1, MaxWidth);
            int height = Clamp(requestedHeight, 1, MaxHeight);
            if (width != requestedWidth || height != requestedHeight)
            {
                warnings.Add(new ObjectEditWarning
                {
                    Code = "VIEWPORT_CAPTURE_SIZE_CLAMPED",
                    Message = $"Requested size {requestedWidth}x{requestedHeight} was clamped to {width}x{height}."
                });
            }

            return CaptureWindowsBitmap(document.Path, resolvedView.Data, width, height, request.BackgroundTransparent, warnings);
        });
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static OperationResponse<ViewportCaptureResponse> CaptureWindowsBitmap(
        string filePath,
        RhinoView view,
        int width,
        int height,
        bool transparentBackground,
        IReadOnlyList<ObjectEditWarning> warnings)
    {
        try
        {
            string outputPath = Path.Combine(Path.GetTempPath(), $"mcp_rhino_viewport_capture_{Guid.NewGuid():N}.png");
            try
            {
                OperationResponse<RhinoCapturedBitmap> captured = RhinoViewBitmapCapture.Capture(
                    view,
                    new Size(width, height),
                    96d,
                    !transparentBackground);
                if (!captured.Success || captured.Data is null)
                {
                    return OperationResponse<ViewportCaptureResponse>.Fail($"Viewport capture failed: {captured.Message}");
                }

                using RhinoCapturedBitmap bitmap = captured.Data;
                OperationResponse saved = bitmap.Save(outputPath, 96d);
                if (!saved.Success)
                {
                    return OperationResponse<ViewportCaptureResponse>.Fail($"Viewport capture failed: {saved.Message}");
                }

                byte[] bytes = File.ReadAllBytes(outputPath);
                if (bytes.Length == 0)
                {
                    return OperationResponse<ViewportCaptureResponse>.Fail("Viewport capture failed: output image was empty.");
                }

                return OperationResponse<ViewportCaptureResponse>.Ok(new ViewportCaptureResponse
                {
                    FilePath = filePath,
                    ViewName = view.MainViewport.Name,
                    Width = width,
                    Height = height,
                    ContentType = "image/png",
                    DataBase64 = Convert.ToBase64String(bytes),
                    ByteCount = bytes.Length,
                    Warnings = warnings
                }, "Viewport captured as base64 PNG from current viewport state.");
            }
            finally
            {
                TryDeleteFile(outputPath);
            }
        }
        catch (Exception ex)
        {
            return OperationResponse<ViewportCaptureResponse>.Fail($"Viewport capture failed: {ex.Message}");
        }
    }

    private static OperationResponse<RhinoView> ResolveView(rhinocommon::Rhino.RhinoDoc document, string? requestedViewName)
    {
        if (string.IsNullOrWhiteSpace(requestedViewName))
        {
            RhinoView? activeView = document.Views.ActiveView;
            return activeView is null
                ? OperationResponse<RhinoView>.Fail("VIEWPORT_NOT_FOUND")
                : OperationResponse<RhinoView>.Ok(activeView);
        }

        RhinoView[] matches = document.Views
            .GetViewList(ViewTypeFilter.All)
            .Where(view => string.Equals(view.MainViewport.Name, requestedViewName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length > 0)
        {
            return OperationResponse<RhinoView>.Ok(matches[0]);
        }

        return OperationResponse<RhinoView>.Fail("VIEWPORT_NOT_FOUND");
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static void TryDeleteFile(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
        catch
        {
        }
    }
}
