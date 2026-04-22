using System.ComponentModel;
using ModelContextProtocol.Server;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class GetDocumentUserStringsInLiveTool
{
    private readonly RhinoDocumentUserStringService _service;

    public GetDocumentUserStringsInLiveTool(RhinoDocumentUserStringService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("读取当前在 Rhino 中打开的文档的 document user strings。需要该文件在 Rhino 会话中处于活动状态；否则返回 LIVE_RHINO_REQUIRED。")]
    public OperationResponse<DocumentUserStringReadResponse> GetDocumentUserStringsInLive(string filePath)
    {
        return _service.ReadInLive(new DocumentUserStringReadRequest { FilePath = filePath });
    }
}
