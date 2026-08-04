using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ICurveEditOrchestrator
{
    OperationResponse<GeometryEditPreviewResponse> Preview(PreviewEditCurveGeometryRequest request);

    OperationResponse<GeometryEditApplyResponse> Apply(ApplyEditCurveGeometryRequest request);
}
