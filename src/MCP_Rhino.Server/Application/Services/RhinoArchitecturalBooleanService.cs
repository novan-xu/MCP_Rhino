using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoArchitecturalBooleanService
{
    private readonly ILiveBooleanOperator _operator;

    public RhinoArchitecturalBooleanService(ILiveBooleanOperator booleanOperator)
    {
        _operator = booleanOperator;
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> Preview(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<ArchitecturalBooleanPreviewResponse>.Fail("At least one boolean operation entry is required.");
        }

        return _operator.Preview(filePath, entries);
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> Apply(
        string filePath,
        IReadOnlyList<ArchitecturalBooleanOperationSpec> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<ArchitecturalBooleanApplyResponse>.Fail("At least one boolean operation entry is required.");
        }

        return _operator.Apply(filePath, entries);
    }

    public OperationResponse<ArchitecturalBooleanPreviewResponse> PreviewOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<ArchitecturalBooleanPreviewResponse>.Fail("At least one opening item is required.");
        }

        return _operator.PreviewOpenings(filePath, entries);
    }

    public OperationResponse<ArchitecturalBooleanApplyResponse> ApplyOpenings(
        string filePath,
        IReadOnlyList<ArchitecturalOpeningOperationSpec> entries,
        ArchitecturalObjectAttributesSpec cutterAttributes)
    {
        if (entries.Count == 0)
        {
            return OperationResponse<ArchitecturalBooleanApplyResponse>.Fail("At least one opening item is required.");
        }

        return _operator.ApplyOpenings(filePath, entries, cutterAttributes);
    }
}

