using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Analysis;

[McpServerToolType]
public sealed class GetDocumentSummaryTool
{
    private readonly RhinoDocumentStateService _service;

    public GetDocumentSummaryTool(RhinoDocumentStateService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read a bounded live summary of the active Rhino document: path, units, tolerances, object/layer counts, current layer, named views, materials, selection count, and optional object summaries.")]
    public OperationResponse<DocumentSummaryResponse> GetDocumentSummary(
        string filePath,
        int maxObjectSummaries = 20,
        int maxLayerSummaries = 100,
        int maxNamedViews = 20,
        int maxMaterials = 20)
    {
        return _service.GetSummary(new GetDocumentSummaryRequest
        {
            FilePath = filePath,
            MaxObjectSummaries = maxObjectSummaries,
            MaxLayerSummaries = maxLayerSummaries,
            MaxNamedViews = maxNamedViews,
            MaxMaterials = maxMaterials
        });
    }
}
