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

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Set document-level user strings in the current live Rhino document by optional section, key, and value.")]
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
