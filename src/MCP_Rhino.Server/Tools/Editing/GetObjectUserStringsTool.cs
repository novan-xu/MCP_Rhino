using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class GetObjectUserStringsTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public GetObjectUserStringsTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("按 ObjectId 列表读取 Rhino 对象的全部 user string，并返回每个对象的图层、名称与 user string 列表。")]
    public OperationResponse<ObjectUserTextReadResponse> GetObjectUserStrings(
        string filePath,
        List<Guid> objectIds)
    {
        return _userTextService.Read(new ObjectUserTextReadRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}
