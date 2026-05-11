using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveBooleanOperator
{
    OperationResponse<ArchitecturalBooleanPreviewResponse> Preview(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries);

    OperationResponse<ArchitecturalBooleanApplyResponse> Apply(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries);

    OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries);

    OperationResponse<ArchitecturalBooleanApplyResponse> ApplyOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries,
        ArchitecturalObjectAttributesSpec cutterAttributes);
}

