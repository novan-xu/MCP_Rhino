using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveStoneWireframeBakeOperator
{
    OperationResponse<StoneWireframeBakeResponse> Preview(
        string filePath,
        IReadOnlyCollection<string> assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5);

    OperationResponse<StoneWireframeBakeResponse> Apply(
        string filePath,
        IReadOnlyCollection<string> assemblyDefinitionNames,
        StoneBakeOutputMode outputMode = StoneBakeOutputMode.Curve,
        double surfaceHeightInches = 5);
}
