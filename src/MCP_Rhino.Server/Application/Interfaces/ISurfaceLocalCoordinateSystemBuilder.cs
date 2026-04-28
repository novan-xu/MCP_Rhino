using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceLocalCoordinateSystemBuilder
{
    OperationResponse<SurfaceLocalCoordinateSystem> Build(
        SurfaceBoundaryLoop boundaryLoop,
        SurfaceReferenceCurveSpec referenceCurve);
}
