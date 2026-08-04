using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.CurveOps;

[McpServerToolType]
public sealed class PreviewSplitCurvesTool
{
    private readonly RhinoCurveDerivedGeometryService _service;

    public PreviewSplitCurvesTool(RhinoCurveDerivedGeometryService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview curve splitting for existing curve object ids in the current live Rhino document. Resolves parameter and point-derived split locations without modifying the document.")]
    public OperationResponse<CurveSplitPreviewResponse> PreviewSplitCurves(
        string filePath,
        List<CurveSplitEntryRequest> entries)
    {
        return _service.PreviewSplitCurves(new PreviewSplitCurvesRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<CurveSplitEntryRequest>()
        });
    }
}
