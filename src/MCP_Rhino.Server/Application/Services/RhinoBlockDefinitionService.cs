using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoBlockDefinitionService
{
    private readonly ILiveBlockDefinitionOperator _operator;

    public RhinoBlockDefinitionService(ILiveBlockDefinitionOperator blockOperator)
    {
        _operator = blockOperator;
    }

    public OperationResponse<BlockDefinitionMutationResponse> CreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionSpec> specs)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<BlockDefinitionMutationResponse>.Fail("At least one block definition item is required.");
        }

        return _operator.CreateDefinitions(filePath, specs);
    }

    public OperationResponse<BlockInstanceCreationResponse> InsertInstances(
        string filePath,
        IReadOnlyList<BlockInstanceSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        if (specs.Count == 0)
        {
            return OperationResponse<BlockInstanceCreationResponse>.Fail("At least one block instance item is required.");
        }

        return _operator.InsertInstances(filePath, specs, attributes);
    }
}

