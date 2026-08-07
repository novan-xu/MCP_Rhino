using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class StartGrasshopperTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public StartGrasshopperTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Load and show Grasshopper for the Router-selected saved Rhino document through Rhino APIs, then return its deterministically bound definition inventory; does not use Windows UI automation.")]
    public OperationResponse<GrasshopperDefinitionListResponse> StartGrasshopper(
        string filePath,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.Start(new GrasshopperEngineRequest { FilePath = filePath, Engine = engine });
}
