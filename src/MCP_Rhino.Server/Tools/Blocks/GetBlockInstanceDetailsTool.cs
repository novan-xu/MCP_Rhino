using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Blocks;

[McpServerToolType]
public sealed class GetBlockInstanceDetailsTool
{
    private readonly RhinoBlockInspectionService _service;

    public GetBlockInstanceDetailsTool(RhinoBlockInspectionService service)
    {
        _service = service;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Get details for one Rhino block instance object.")]
    public OperationResponse<BlockInstanceDetailResponse> GetBlockInstanceDetails(
        string filePath,
        Guid objectId)
    {
        return _service.GetInstanceDetails(new GetBlockInstanceDetailsRequest
        {
            FilePath = filePath,
            ObjectId = objectId
        });
    }
}
