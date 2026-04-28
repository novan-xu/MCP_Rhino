using System.ComponentModel;
using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Tools.Geometry.Edit;

[McpServerToolType]
public sealed class GetEditableGeometryDescriptorTool
{
    private readonly IEditableGeometryDescriptorService _service;

    public GetEditableGeometryDescriptorTool(IEditableGeometryDescriptorService service)
    {
        _service = service;
    }

    [McpServerTool]
    [Description("Read the editable descriptor for a live Rhino curve, surface, or untrimmed single-face Brep. Requires the file to be the active saved Rhino document; otherwise returns LIVE_RHINO_REQUIRED.")]
    public OperationResponse<EditableGeometryDescriptorResponse> GetEditableGeometryDescriptor(
        string filePath,
        Guid objectId,
        DescriptorDetail? detail = null)
    {
        return _service.Read(new GetEditableGeometryDescriptorRequest
        {
            FilePath = filePath,
            ObjectId = objectId,
            Detail = detail
        });
    }
}
