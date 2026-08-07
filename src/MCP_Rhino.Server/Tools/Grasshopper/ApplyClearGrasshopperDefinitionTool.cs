using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class ApplyClearGrasshopperDefinitionTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public ApplyClearGrasshopperDefinitionTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Destructively clear exactly the previously previewed revision of one explicit live Grasshopper definition using its bound preview token and one native Grasshopper undo entry; rejects stale definitions.")]
    public OperationResponse<GrasshopperClearApplyResponse> ApplyClearGrasshopperDefinition(
        string filePath,
        string definitionSessionId,
        string previewToken,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.ApplyClear(new ApplyClearGrasshopperDefinitionRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId,
            PreviewToken = previewToken
        });
}
