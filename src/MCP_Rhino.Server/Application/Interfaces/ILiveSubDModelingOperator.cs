using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveSubDModelingOperator
{
    OperationResponse<SubDCreationResponse> Create(
        string filePath,
        IReadOnlyList<SubDCageSpec> cages,
        GeometryObjectAttributesSpec attributes,
        SubDModelingOperationKind operation);

    OperationResponse<SubDInspectionResponse> Inspect(
        string filePath,
        IReadOnlyList<Guid> objectIds);
}
