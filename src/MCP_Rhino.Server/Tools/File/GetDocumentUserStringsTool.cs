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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read all document-level user strings from the current live Rhino document.")]
    public OperationResponse<DocumentUserStringReadResponse> GetDocumentUserStrings(string filePath)
    {
        return _service.Read(new DocumentUserStringReadRequest
        {
            FilePath = filePath
        });
    }
}
