using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IGeometryEditStrategyResolver
{
    OperationResponse<StrategyResolutionResult> Resolve(CurveEditSpec editSpec, EditableGeometryDescriptor descriptor);

    OperationResponse<StrategyResolutionResult> ResolveSurface(SurfaceEditSpec editSpec, EditableGeometryDescriptor descriptor);
}
