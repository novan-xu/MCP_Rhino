using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class PreviewTakeoffScheduleTool
{
    private readonly RhinoTakeoffScheduleService _service;

    public PreviewTakeoffScheduleTool(RhinoTakeoffScheduleService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview a flexible live Rhino take-off schedule from a validated structured spec. Resolves scoped objects, reads user text and geometry metrics, groups/sorts/aggregates rows, and returns samples/warnings without writing a spreadsheet.")]
    public OperationResponse<TakeoffSchedulePreviewResponse> PreviewTakeoffSchedule(
        string filePath,
        TakeoffScheduleSpecRequest spec,
        int sampleRowCount = 20)
    {
        return _service.Preview(new PreviewTakeoffScheduleRequest
        {
            FilePath = filePath,
            Spec = spec,
            SampleRowCount = sampleRowCount
        });
    }
}
