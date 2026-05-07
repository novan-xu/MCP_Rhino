using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class ExportToImageTool
{
    private readonly RhinoFileExportService _service;

    public ExportToImageTool(RhinoFileExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Capture a named Rhino view to an image file (.jpg/.png/.bmp/.tiff). This is live-only and reads the current viewport state without mutating the document.")]
    public OperationResponse<FileExportResponse> ExportToImage(
        string filePath,
        string outputPath,
        string? viewName = null,
        ImageSizePxRequest? imageSizePx = null,
        double? dotsPerInch = null,
        bool backgroundTransparent = false,
        bool overwriteExisting = true)
    {
        return _service.ExportToImage(new ExportToImageRequest
        {
            FilePath = filePath,
            OutputPath = outputPath,
            ViewName = viewName,
            ImageSizePx = imageSizePx,
            DotsPerInch = dotsPerInch,
            BackgroundTransparent = backgroundTransparent,
            OverwriteExisting = overwriteExisting
        });
    }
}
