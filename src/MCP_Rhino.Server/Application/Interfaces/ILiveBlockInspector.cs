using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveBlockInspector
{
    OperationResponse<BlockDefinitionListResponse> ListDefinitions(
        string filePath,
        bool includeLinked,
        bool includeNestedSummary);

    OperationResponse<BlockDefinitionDetailResponse> GetDefinitionDetails(
        string filePath,
        string definitionName,
        Guid definitionId,
        bool includeNestedSummary);

    OperationResponse<BlockInstanceListResponse> ListInstances(
        string filePath,
        string definitionName,
        bool includeHidden);

    OperationResponse<BlockInstanceDetailResponse> GetInstanceDetails(
        string filePath,
        Guid objectId);
}
