using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryEditValidator
{
    OperationResponse ValidateRequest(Guid objectId, CurveEditSpec editSpec);

    OperationResponse ValidatePointInputs(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor);

    OperationResponse ValidateSurfaceRequest(Guid objectId, SurfaceEditSpec editSpec);

    OperationResponse ValidateSurfacePointInputs(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor);
}
