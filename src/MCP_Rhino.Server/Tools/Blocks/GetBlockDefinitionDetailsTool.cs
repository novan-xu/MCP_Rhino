using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class GetBlockDefinitionDetailsTool
{
    private readonly RhinoBlockInspectionService _service;

    public GetBlockDefinitionDetailsTool(RhinoBlockInspectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get details for one Rhino block definition by name or definition id.")]
    public OperationResponse<BlockDefinitionDetailResponse> GetBlockDefinitionDetails(
        string filePath,
        string definitionName = "",
        Guid definitionId = default,
        bool includeNestedSummary = true)
    {
        return _service.GetDefinitionDetails(new GetBlockDefinitionDetailsRequest
        {
            FilePath = filePath,
            DefinitionName = definitionName,
            DefinitionId = definitionId,
            IncludeNestedSummary = includeNestedSummary
        });
    }
}
