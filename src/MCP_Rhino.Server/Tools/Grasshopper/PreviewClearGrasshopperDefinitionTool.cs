using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class PreviewClearGrasshopperDefinitionTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public PreviewClearGrasshopperDefinitionTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview clearing one explicit live Grasshopper definition, returning the exact object ids, wire count, revision, and bound preview token without mutation or solution execution.")]
    public OperationResponse<GrasshopperClearPreviewResponse> PreviewClearGrasshopperDefinition(
        string filePath,
        string definitionSessionId,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.PreviewClear(new GrasshopperDefinitionRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId
        });
}
