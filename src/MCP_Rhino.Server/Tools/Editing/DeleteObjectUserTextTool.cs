using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class DeleteObjectUserTextTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public DeleteObjectUserTextTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool(ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Delete object user text entries from the current live Rhino document by ObjectId and key.")]
    public OperationResponse<ObjectEditExecutionResponse> DeleteObjectUserText(
        string filePath,
        List<ObjectScopedUserTextKeyRequest> entries)
    {
        return _userTextService.Delete(new ObjectUserTextDeleteRequest
        {
            FilePath = filePath,
            Entries = entries ?? new List<ObjectScopedUserTextKeyRequest>()
        });
    }
}
