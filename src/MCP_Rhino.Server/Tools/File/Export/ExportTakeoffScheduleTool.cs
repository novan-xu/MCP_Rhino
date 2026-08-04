using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Export;

[McpServerToolType]
public sealed class ExportTakeoffScheduleTool
{
    private readonly RhinoTakeoffScheduleService _service;

    public ExportTakeoffScheduleTool(RhinoTakeoffScheduleService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Export a flexible live Rhino take-off schedule to an explicitly supplied CSV or XLSX file. Requires outputDirectory and outputFileName, validates overwrite rules, resolves live objects, calculates the schedule, and writes only the requested spreadsheet.")]
    public OperationResponse<TakeoffScheduleExportResponse> ExportTakeoffSchedule(
        string filePath,
        string outputDirectory,
        string outputFileName,
        TakeoffScheduleSpecRequest spec,
        bool overwriteExisting = false)
    {
        return _service.Export(new ExportTakeoffScheduleRequest
        {
            FilePath = filePath,
            OutputDirectory = outputDirectory,
            OutputFileName = outputFileName,
            OverwriteExisting = overwriteExisting,
            Spec = spec
        });
    }
}
