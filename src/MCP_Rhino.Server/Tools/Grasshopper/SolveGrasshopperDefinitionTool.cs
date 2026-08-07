using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class SolveGrasshopperDefinitionTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public SolveGrasshopperDefinitionTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Solve one explicit live Grasshopper definition and return object-level warnings/errors/faults plus optional bounded output samples; installed third-party and script/code components can execute external code.")]
    public OperationResponse<GrasshopperSolveResponse> SolveGrasshopperDefinition(
        string filePath,
        string definitionSessionId,
        bool expireAllObjects = true,
        int dataSampleSize = 0,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.Solve(new SolveGrasshopperDefinitionRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId,
            ExpireAllObjects = expireAllObjects,
            DataSampleSize = dataSampleSize
        });
}
