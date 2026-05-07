using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class PreviewDeleteLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public PreviewDeleteLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview layer deletion impact against the active Rhino document without changing it.")]
    public OperationResponse<LayerDeletionPreviewResponse> PreviewDeleteLayers(string filePath, List<string> fullPaths)
    {
        return _service.PreviewDelete(new PreviewDeleteLayersRequest
        {
            FilePath = filePath,
            FullPaths = fullPaths ?? new List<string>()
        });
    }
}
