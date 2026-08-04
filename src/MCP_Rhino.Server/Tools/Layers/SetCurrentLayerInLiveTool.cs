using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class SetCurrentLayerInLiveTool
{
    private readonly RhinoDocumentStateService _service;

    public SetCurrentLayerInLiveTool(RhinoDocumentStateService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Set the current layer in the live Rhino document by layer id, confirmed full path, or unambiguous layer query. This changes UI/document state but not geometry.")]
    public OperationResponse<CurrentLayerMutationResponse> SetCurrentLayerInLive(
        string filePath,
        Guid? layerId = null,
        string? fullPath = null,
        string? layerQuery = null,
        bool exactMatch = true)
    {
        return _service.SetCurrentLayer(new SetCurrentLayerInLiveRequest
        {
            FilePath = filePath,
            LayerId = layerId,
            FullPath = fullPath,
            LayerQuery = layerQuery,
            ExactMatch = exactMatch
        });
    }
}
