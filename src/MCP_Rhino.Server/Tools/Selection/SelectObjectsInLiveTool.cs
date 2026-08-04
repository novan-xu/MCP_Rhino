using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Selection;

[McpServerToolType]
public sealed class SelectObjectsInLiveTool
{
    private readonly RhinoSelectionService _service;

    public SelectObjectsInLiveTool(RhinoSelectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Set Rhino UI object selection in the live document. Supports explicit ObjectIds, optional filter criteria, and Replace/Add/Remove/Clear modes; does not change geometry.")]
    public OperationResponse<SelectionMutationResponse> SelectObjectsInLive(
        string filePath,
        List<Guid>? objectIds = null,
        FilterObjectsRequest? filter = null,
        RhinoSelectionMode selectionMode = RhinoSelectionMode.Replace)
    {
        return _service.SelectObjects(new SelectObjectsInLiveRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>(),
            Filter = filter,
            SelectionMode = selectionMode
        });
    }
}
