using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class GetLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public GetLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read all layers from the current live Rhino document, including object counts and layer properties.")]
    public OperationResponse<LayerReadResponse> GetLayers(string filePath)
    {
        return _service.Get(new GetLayersRequest
        {
            FilePath = filePath
        });
    }
}
