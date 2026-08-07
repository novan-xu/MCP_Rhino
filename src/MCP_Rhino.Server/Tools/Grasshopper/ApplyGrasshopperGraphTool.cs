using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class ApplyGrasshopperGraphTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public ApplyGrasshopperGraphTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Apply exactly a previously previewed typed Grasshopper graph batch to one explicit live definition, including installed script/code components, sliders, and wires, with stale-revision rejection, one GH undo entry, rollback, and optional single solve.")]
    public OperationResponse<GrasshopperGraphApplyResponse> ApplyGrasshopperGraph(
        string filePath,
        string definitionSessionId,
        GrasshopperGraphSpec graph,
        string previewToken,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.ApplyGraph(new ApplyGrasshopperGraphRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId,
            Graph = graph,
            PreviewToken = previewToken
        });
}
