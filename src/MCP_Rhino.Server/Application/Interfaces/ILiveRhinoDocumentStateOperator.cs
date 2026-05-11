using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveRhinoDocumentStateOperator
{
    OperationResponse<DocumentSummaryResponse> GetSummary(GetDocumentSummaryRequest request);

    OperationResponse<CurrentLayerResponse> GetCurrentLayer(GetCurrentLayerInLiveRequest request);

    OperationResponse<CurrentLayerMutationResponse> SetCurrentLayer(SetCurrentLayerInLiveRequest request);
}
