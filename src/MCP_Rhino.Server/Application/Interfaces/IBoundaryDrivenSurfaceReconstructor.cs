using MCP_Rhino.Server.Application.Models;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface IBoundaryDrivenSurfaceReconstructor
{
    OperationResponse<BoundaryRebuildResult> Reconstruct(
        SurfacePointOrderPlan plan,
        SurfaceBoundaryLoop boundaryLoop);
}
