using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class ListBlockDefinitionsTool
{
    private readonly RhinoBlockInspectionService _service;

    public ListBlockDefinitionsTool(RhinoBlockInspectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List Rhino block definitions in the active live document.")]
    public OperationResponse<BlockDefinitionListResponse> ListBlockDefinitions(
        string filePath,
        bool includeLinked = true,
        bool includeNestedSummary = true)
    {
        return _service.ListDefinitions(new ListBlockDefinitionsRequest
        {
            FilePath = filePath,
            IncludeLinked = includeLinked,
            IncludeNestedSummary = includeNestedSummary
        });
    }
}
