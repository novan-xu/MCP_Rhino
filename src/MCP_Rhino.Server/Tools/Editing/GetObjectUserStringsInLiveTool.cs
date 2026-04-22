using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class GetObjectUserStringsInLiveTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public GetObjectUserStringsInLiveTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool]
    [Description("按 objectId 读取当前 Rhino 活动文档中物体的 user strings。需要文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。")]
    public OperationResponse<ObjectUserTextReadResponse> GetObjectUserStringsInLive(
        string filePath,
        List<Guid> objectIds)
    {
        return _userTextService.ReadInLive(new ObjectUserTextReadRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}
