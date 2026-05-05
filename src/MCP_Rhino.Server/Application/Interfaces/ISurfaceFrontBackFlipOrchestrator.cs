using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceFrontBackFlipOrchestrator
{
    OperationResponse<SurfaceFrontBackFlipApplyResponse> Apply(ApplySurfaceFrontBackFlipRequest request);
}
