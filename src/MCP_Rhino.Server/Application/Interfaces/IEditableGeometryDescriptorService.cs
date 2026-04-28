using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IEditableGeometryDescriptorService
{
    OperationResponse<EditableGeometryDescriptorResponse> Read(GetEditableGeometryDescriptorRequest request);
}
