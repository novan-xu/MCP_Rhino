using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ISurfaceEditOrchestrator
{
    OperationResponse<GeometryEditPreviewResponse> Preview(PreviewEditSurfaceGeometryRequest request);

    OperationResponse<GeometryEditApplyResponse> Apply(ApplyEditSurfaceGeometryRequest request);
}
