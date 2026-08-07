using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models.Grasshopper;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Grasshopper;

[McpServerToolType]
public sealed class DescribeGrasshopperComponentTool
{
    private readonly RhinoGrasshopperAuthoringService _service;
    public DescribeGrasshopperComponentTool(RhinoGrasshopperAuthoringService service) => _service = service;

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("Describe normalized input and output ports for an exact installed Grasshopper component GUID without adding it to a definition; construction of third-party or code-component instances may execute external code.")]
    public OperationResponse<GrasshopperComponentDescriptionResponse> DescribeGrasshopperComponent(
        string filePath,
        Guid componentGuid,
        GrasshopperEngine engine = GrasshopperEngine.Gh1) =>
        _service.DescribeComponent(new DescribeGrasshopperComponentRequest
        {
            FilePath = filePath,
            Engine = engine,
            ComponentGuid = componentGuid
        });
}
