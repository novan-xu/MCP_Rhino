using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class PreviewModifyLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public PreviewModifyLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Preview layer modifications against the active Rhino document without changing it.")]
    public OperationResponse<LayerModificationPreviewResponse> PreviewModifyLayers(string filePath, List<LayerModificationEntryRequest> entries)
    {
        return _service.PreviewModify(new PreviewModifyLayersRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<LayerModificationEntryRequest>()
        });
    }
}
