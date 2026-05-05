using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class GetDocumentUserStringsTool
{
    private readonly RhinoDocumentUserStringService _service;

    public GetDocumentUserStringsTool(RhinoDocumentUserStringService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("读取当前 Rhino 文档的全部文档级 user string。")]
    public OperationResponse<DocumentUserStringReadResponse> GetDocumentUserStrings(string filePath)
    {
        return _service.Read(new DocumentUserStringReadRequest
        {
            FilePath = filePath
        });
    }
}
