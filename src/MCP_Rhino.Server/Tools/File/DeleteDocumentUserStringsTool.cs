using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File;

[McpServerToolType]
public sealed class DeleteDocumentUserStringsTool
{
    private readonly RhinoDocumentUserStringService _service;

    public DeleteDocumentUserStringsTool(RhinoDocumentUserStringService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("批量删除当前 Rhino 文档的文档级 user string。每条 entry 仅需 Section（可空）与 Key。")]
    public OperationResponse<DocumentUserStringMutationResponse> DeleteDocumentUserStrings(
        string filePath,
        List<DocumentUserStringEntryRequest> entries)
    {
        return _service.Delete(new DocumentUserStringDeleteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<DocumentUserStringEntryRequest>()
        });
    }
}
