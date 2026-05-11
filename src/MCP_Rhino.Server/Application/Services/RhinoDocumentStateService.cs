using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoDocumentStateService
{
    private readonly ILiveRhinoDocumentStateOperator _operator;

    public RhinoDocumentStateService(ILiveRhinoDocumentStateOperator @operator)
    {
        _operator = @operator;
    }

    public OperationResponse<DocumentSummaryResponse> GetSummary(GetDocumentSummaryRequest request)
    {
        return _operator.GetSummary(request);
    }

    public OperationResponse<CurrentLayerResponse> GetCurrentLayer(GetCurrentLayerInLiveRequest request)
    {
        return _operator.GetCurrentLayer(request);
    }

    public OperationResponse<CurrentLayerMutationResponse> SetCurrentLayer(SetCurrentLayerInLiveRequest request)
    {
        return _operator.SetCurrentLayer(request);
    }
}
