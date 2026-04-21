using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class CreateLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public CreateLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Create layers in the active Rhino document. Missing parent layers are auto-created.")]
    public OperationResponse<LayerMutationResponse> CreateLayers(string filePath, List<LayerCreationEntryRequest> entries)
    {
        return _service.Create(new CreateLayersRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<LayerCreationEntryRequest>()
        });
    }
}
