using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class PurgeLayersTool
{
    private readonly RhinoLayerManagementService _service;

    public PurgeLayersTool(RhinoLayerManagementService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Purge layer subtrees from the active Rhino document and remove all objects on those layers.")]
    public OperationResponse<LayerMutationResponse> PurgeLayers(string filePath, List<string> fullPaths)
    {
        return _service.Purge(new PurgeLayersRequest
        {
            FilePath = filePath,
            FullPaths = fullPaths ?? new List<string>()
        });
    }
}
