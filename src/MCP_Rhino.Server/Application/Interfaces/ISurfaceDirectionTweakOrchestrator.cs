using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceDirectionTweakOrchestrator
{
    OperationResponse<SurfaceDirectionTweakPreviewResponse> Preview(PreviewSurfaceDirectionTweakRequest request);

    OperationResponse<SurfaceDirectionTweakApplyResponse> Apply(ApplySurfaceDirectionTweakRequest request);
}
