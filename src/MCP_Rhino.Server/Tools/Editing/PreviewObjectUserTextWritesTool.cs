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

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Preview object user text writes for explicit ObjectIds in the current live Rhino document without mutating the document.")]
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
