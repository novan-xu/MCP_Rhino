using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Reference;

[McpServerToolType]
public sealed class UpdateLinkedBlockTool
{
    private readonly RhinoExternalReferenceService _service;

    public UpdateLinkedBlockTool(RhinoExternalReferenceService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Refresh one or more existing linked block definitions in the saved active Rhino document. This is live-only and creates a Rhino undo step when updates succeed.")]
    public OperationResponse<LinkedBlockMutationResponse> UpdateLinkedBlock(string filePath, List<string> definitionNames)
    {
        return _service.UpdateLinkedBlock(new UpdateLinkedBlockRequest
        {
            FilePath = filePath,
            DefinitionNames = definitionNames ?? new List<string>()
        });
    }
}
