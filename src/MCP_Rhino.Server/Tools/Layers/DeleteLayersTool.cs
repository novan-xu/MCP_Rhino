using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class DeleteLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public DeleteLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Delete layer subtrees in the active Rhino document and move affected objects to the target parent layer.")]
    public OperationResponse<LayerMutationResponse> DeleteLayers(string filePath, List<string> fullPaths)
    {
        return _service.Delete(new DeleteLayersRequest
        {
            FilePath = filePath,
            FullPaths = fullPaths ?? new List<string>()
        });
    }
}
