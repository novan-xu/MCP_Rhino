using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Layers;

[McpServerToolType]
public sealed class GetCurrentLayerInLiveTool
{
    private readonly RhinoDocumentStateService _service;

    public GetCurrentLayerInLiveTool(RhinoDocumentStateService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read the current layer from the active live Rhino document, including layer id, index, full path, visibility, lock state, and object count.")]
    public OperationResponse<CurrentLayerResponse> GetCurrentLayerInLive(string filePath)
    {
        return _service.GetCurrentLayer(new GetCurrentLayerInLiveRequest { FilePath = filePath });
    }
}
