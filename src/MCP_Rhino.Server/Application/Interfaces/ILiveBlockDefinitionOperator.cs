using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveBlockDefinitionOperator
{
    OperationResponse<BlockDefinitionMutationResponse> CreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionSpec> specs);

    OperationResponse<BlockInstanceCreationResponse> InsertInstances(
        string filePath,
        IReadOnlyList<BlockInstanceSpec> specs,
        ArchitecturalObjectAttributesSpec attributes);
}

