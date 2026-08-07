using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class ListGrasshopperDefinitionsTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public ListGrasshopperDefinitionsTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List all live Grasshopper definitions provably associated with the Router-selected saved Rhino document, returning opaque definition session ids; never follows the active canvas.")]
    public OperationResponse<GrasshopperDefinitionListResponse> ListGrasshopperDefinitions(
        string filePath,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.ListDefinitions(new GrasshopperEngineRequest { FilePath = filePath, Engine = engine });
}
