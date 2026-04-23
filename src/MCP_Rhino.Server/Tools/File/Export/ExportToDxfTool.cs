using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class ExportToDxfTool
{
    private readonly RhinoFileExportService _service;

    public ExportToDxfTool(RhinoFileExportService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Export the saved active Rhino document to DXF through RhinoDoc.WriteFile. This is live-only and does not modify document geometry or document path.")]
    public OperationResponse<FileExportResponse> ExportToDxf(string filePath, string outputPath, List<Guid>? selectedObjectIds = null, bool overwriteExisting = true, Dictionary<string, string>? formatOptions = null)
    {
        return _service.ExportToDxf(new ExportToDxfRequest
        {
            FilePath = filePath,
            OutputPath = outputPath,
            SelectedObjectIds = selectedObjectIds ?? new List<Guid>(),
            OverwriteExisting = overwriteExisting,
            FormatOptions = formatOptions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        });
    }
}
