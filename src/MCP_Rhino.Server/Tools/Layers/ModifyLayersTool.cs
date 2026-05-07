using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class ModifyLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public ModifyLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Modify layer properties, rename layers, or reparent layers in the active Rhino document.")]
    public OperationResponse<LayerMutationResponse> ModifyLayers(string filePath, List<LayerModificationEntryRequest> entries)
    {
        return _service.Modify(new ModifyLayersRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<LayerModificationEntryRequest>()
        });
    }
}
