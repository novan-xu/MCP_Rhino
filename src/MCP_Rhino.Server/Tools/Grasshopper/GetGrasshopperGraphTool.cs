using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class GetGrasshopperGraphTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public GetGrasshopperGraphTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read nodes, wires, runtime diagnostics, revision, and optionally bounded volatile-data samples from one explicit live Grasshopper definition session; never reads a .gh/.ghx file or active canvas.")]
    public OperationResponse<GrasshopperGraphResponse> GetGrasshopperGraph(
        string filePath,
        string definitionSessionId,
        int dataSampleSize = 0,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.GetGraph(new GetGrasshopperGraphRequest
        {
            FilePath = filePath,
            Engine = engine,
            DefinitionSessionId = definitionSessionId,
            DataSampleSize = dataSampleSize
        });
}
