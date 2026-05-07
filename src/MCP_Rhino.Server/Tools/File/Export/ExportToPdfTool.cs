using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class ExportToPdfTool
{
    private readonly RhinoFileExportService _service;

    public ExportToPdfTool(RhinoFileExportService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Capture a named Rhino view to PDF via Rhino FilePdf. This is live-only and preserves the active document path.")]
    public OperationResponse<FileExportResponse> ExportToPdf(
        string filePath,
        string outputPath,
        string? viewName = null,
        PageSizeMmRequest? pageSizeMm = null,
        double? dotsPerInch = null,
        bool overwriteExisting = true)
    {
        return _service.ExportToPdf(new ExportToPdfRequest
        {
            FilePath = filePath,
            OutputPath = outputPath,
            ViewName = viewName,
            PageSizeMm = pageSizeMm,
            DotsPerInch = dotsPerInch,
            OverwriteExisting = overwriteExisting
        });
    }
}
