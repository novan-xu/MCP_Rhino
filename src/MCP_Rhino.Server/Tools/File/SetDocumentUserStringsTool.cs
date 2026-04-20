using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class SetDocumentUserStringsTool
{
    private readonly RhinoDocumentUserStringService _service;

    public SetDocumentUserStringsTool(RhinoDocumentUserStringService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("批量写入 Rhino 文件的文档级 user string。每条 entry 可指定 Section（可空）、Key、Value，对应 File3dmStringTable.SetString。")]
    public OperationResponse<DocumentUserStringMutationResponse> SetDocumentUserStrings(
        string filePath,
        List<DocumentUserStringEntryRequest> entries)
    {
        return _service.Set(new DocumentUserStringWriteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<DocumentUserStringEntryRequest>()
        });
    }
}
