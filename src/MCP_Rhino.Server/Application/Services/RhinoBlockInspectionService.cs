using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoBlockInspectionService
{
    private readonly ILiveBlockInspector _inspector;

    public RhinoBlockInspectionService(ILiveBlockInspector inspector)
    {
        _inspector = inspector;
    }

    public OperationResponse<BlockDefinitionListResponse> ListDefinitions(ListBlockDefinitionsRequest request)
    {
        return _inspector.ListDefinitions(
            request.FilePath,
            request.IncludeLinked,
            request.IncludeNestedSummary);
    }

    public OperationResponse<BlockDefinitionDetailResponse> GetDefinitionDetails(GetBlockDefinitionDetailsRequest request)
    {
        if (request.DefinitionId == Guid.Empty && string.IsNullOrWhiteSpace(request.DefinitionName))
        {
            return OperationResponse<BlockDefinitionDetailResponse>.Fail("DefinitionName or DefinitionId is required.");
        }

        return _inspector.GetDefinitionDetails(
            request.FilePath,
            request.DefinitionName,
            request.DefinitionId,
            request.IncludeNestedSummary);
    }

    public OperationResponse<BlockInstanceListResponse> ListInstances(ListBlockInstancesRequest request)
    {
        return _inspector.ListInstances(
            request.FilePath,
            request.DefinitionName,
            request.IncludeHidden);
    }

    public OperationResponse<BlockInstanceDetailResponse> GetInstanceDetails(GetBlockInstanceDetailsRequest request)
    {
        if (request.ObjectId == Guid.Empty)
        {
            return OperationResponse<BlockInstanceDetailResponse>.Fail("ObjectId is required.");
        }

        return _inspector.GetInstanceDetails(request.FilePath, request.ObjectId);
    }
}
