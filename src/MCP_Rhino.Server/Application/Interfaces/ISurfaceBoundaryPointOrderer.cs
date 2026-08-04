using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceBoundaryPointOrderer
{
    OperationResponse<SurfacePointOrderPlan> Order(
        SurfaceRebuildDescriptor descriptor,
        SurfaceReferenceCurveSpec referenceCurve,
        SurfaceLocalCoordinateSystem localFrame,
        SurfaceRebuildSpec spec);
}
