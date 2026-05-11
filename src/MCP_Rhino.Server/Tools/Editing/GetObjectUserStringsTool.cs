using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Editing;

[McpServerToolType]
public sealed class GetObjectUserStringsTool
{
    private readonly RhinoObjectUserTextService _userTextService;

    public GetObjectUserStringsTool(RhinoObjectUserTextService userTextService)
    {
        _userTextService = userTextService;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read all object user strings for explicit ObjectIds in the current live Rhino document, including each object's layer and name.")]
    public OperationResponse<ObjectUserTextReadResponse> GetObjectUserStrings(
        string filePath,
        List<Guid> objectIds)
    {
        return _userTextService.Read(new ObjectUserTextReadRequest
        {
            FilePath = filePath,
            ObjectIds = objectIds ?? new List<Guid>()
        });
    }
}
