using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class PreviewObjectUserTextWritesInLiveTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public PreviewObjectUserTextWritesInLiveTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool]
    [Description("针对当前 Rhino 活动文档预览物体 user string 写入（不做变更）。需要文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。")]
    public OperationResponse<ObjectEditPreviewResponse> PreviewObjectUserTextWritesInLive(
        string filePath,
        List<ObjectScopedUserTextEntryRequest> entries)
    {
        return _userTextService.PreviewInLive(new ObjectUserTextBatchWriteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ObjectScopedUserTextEntryRequest>()
        });
    }
}
