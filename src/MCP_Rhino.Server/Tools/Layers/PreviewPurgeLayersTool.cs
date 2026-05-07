using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class PreviewPurgeLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public PreviewPurgeLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview layer purge impact against the active Rhino document without changing it.")]
    public OperationResponse<LayerDeletionPreviewResponse> PreviewPurgeLayers(string filePath, List<string> fullPaths)
    {
        return _service.PreviewPurge(new PreviewPurgeLayersRequest
        {
            FilePath = filePath,
            FullPaths = fullPaths ?? new List<string>()
        });
    }
}
