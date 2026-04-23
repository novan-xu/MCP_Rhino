using System.ComponentModel;
using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.File.Reference;

[McpServerToolType]
public sealed class ListWorksessionAttachmentsTool
{
    private readonly RhinoExternalReferenceService _service;

    public ListWorksessionAttachmentsTool(RhinoExternalReferenceService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("List the current worksession attachments of the saved active Rhino document. This is live-only and read-only.")]
    public OperationResponse<WorksessionAttachmentResponse> ListWorksessionAttachments(string filePath)
    {
        return _service.ListWorksessionAttachments(new ListWorksessionAttachmentsRequest
        {
            FilePath = filePath
        });
    }
}
