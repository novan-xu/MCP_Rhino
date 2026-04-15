using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class PreviewObjectUserTextWritesTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public PreviewObjectUserTextWritesTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool]
    [Description("预览按对象批量写入 Rhino user text。每条 entry 直接指定 ObjectId、Key、Value，可承接其他工具/技能/外部端口产出的对象级信息。")]
    public OperationResponse<ObjectEditPreviewResponse> PreviewObjectUserTextWrites(
        string filePath,
        List<ObjectScopedUserTextEntryRequest> entries)
    {
        return _userTextService.Preview(new ObjectUserTextBatchWriteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ObjectScopedUserTextEntryRequest>()
        });
    }
}