using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class ApplyObjectUserTextWritesTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public ApplyObjectUserTextWritesTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Apply object user text writes in the current live Rhino document. Each entry targets one ObjectId, key, and value.")]
    public OperationResponse<ObjectEditExecutionResponse> ApplyObjectUserTextWrites(
        string filePath,
        List<ObjectScopedUserTextEntryRequest> entries)
    {
        return _userTextService.Apply(new ObjectUserTextBatchWriteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ObjectScopedUserTextEntryRequest>()
        });
    }
}
