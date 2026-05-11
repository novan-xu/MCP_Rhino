using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ListBlockInstancesTool
{
    private readonly RhinoBlockInspectionService _service;

    public ListBlockInstancesTool(RhinoBlockInspectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List Rhino block instances in the active live document.")]
    public OperationResponse<BlockInstanceListResponse> ListBlockInstances(
        string filePath,
        string definitionName = "",
        bool includeHidden = false)
    {
        return _service.ListInstances(new ListBlockInstancesRequest
        {
            FilePath = filePath,
            DefinitionName = definitionName,
            IncludeHidden = includeHidden
        });
    }
}
