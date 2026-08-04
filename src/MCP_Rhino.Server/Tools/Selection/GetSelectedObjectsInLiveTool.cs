using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Selection;

[McpServerToolType]
public sealed class GetSelectedObjectsInLiveTool
{
    private readonly RhinoSelectionService _service;

    public GetSelectedObjectsInLiveTool(RhinoSelectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read the currently selected Rhino objects from the live document and return object ids, types, layers, names, and user attributes.")]
    public OperationResponse<SelectedObjectsResponse> GetSelectedObjectsInLive(string filePath)
    {
        return _service.GetSelectedObjects(new GetSelectedObjectsInLiveRequest { FilePath = filePath });
    }
}
