using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceRebuildOrchestrator
{
    OperationResponse<SurfaceRebuildDescriptorResponse> Inspect(InspectSurfaceRebuildDescriptorRequest request);

    OperationResponse<SurfacePointOrderPreviewResponse> Preview(PreviewRedefineSurfacePointOrderRequest request);

    OperationResponse<SurfacePointOrderApplyResponse> Apply(ApplyRedefineSurfacePointOrderRequest request);
}
