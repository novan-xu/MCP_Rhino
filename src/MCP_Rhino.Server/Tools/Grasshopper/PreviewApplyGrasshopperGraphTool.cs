using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class PreviewApplyGrasshopperGraphTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public PreviewApplyGrasshopperGraphTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("Resolve and validate a typed batch of Grasshopper components, installed script/code components, number sliders, and wires against an explicit definition without mutating it; returns resolved identities, executable-code warnings, revision, and a bound preview token.")]
    public OperationResponse<GrasshopperGraphPreviewResponse> PreviewApplyGrasshopperGraph(
        string filePath,
        string definitionSessionId,
        GrasshopperGraphSpec graph,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.PreviewGraph(new PreviewApplyGrasshopperGraphRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId,
            Graph = graph
        });
}
