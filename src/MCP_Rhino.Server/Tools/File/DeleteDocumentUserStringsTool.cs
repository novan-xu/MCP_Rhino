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

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Delete document-level user strings from the current live Rhino document by optional section and key.")]
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
