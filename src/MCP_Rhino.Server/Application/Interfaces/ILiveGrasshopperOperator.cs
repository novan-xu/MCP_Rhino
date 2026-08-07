using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveGrasshopperOperator
{
    OperationResponse<GrasshopperDefinitionListResponse> Start(GrasshopperEngineRequest request);
    OperationResponse<GrasshopperDefinitionListResponse> ListDefinitions(GrasshopperEngineRequest request);
    OperationResponse<GrasshopperComponentSearchResponse> SearchComponents(SearchGrasshopperComponentsRequest request);
    OperationResponse<GrasshopperComponentDescriptionResponse> DescribeComponent(DescribeGrasshopperComponentRequest request);
    OperationResponse<GrasshopperGraphResponse> GetGraph(GetGrasshopperGraphRequest request);
    OperationResponse<GrasshopperGraphPreviewResponse> PreviewGraph(PreviewApplyGrasshopperGraphRequest request);
    OperationResponse<GrasshopperGraphApplyResponse> ApplyGraph(
        ApplyGrasshopperGraphRequest request,
        GrasshopperGraphPreviewResponse preview);
    OperationResponse<GrasshopperSolveResponse> Solve(SolveGrasshopperDefinitionRequest request);
    OperationResponse<GrasshopperClearPreviewResponse> PreviewClear(GrasshopperDefinitionRequest request);
    OperationResponse<GrasshopperClearApplyResponse> ApplyClear(
        ApplyClearGrasshopperDefinitionRequest request,
        GrasshopperClearPreviewResponse preview);
}
