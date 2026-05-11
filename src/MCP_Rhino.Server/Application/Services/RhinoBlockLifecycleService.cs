using MCP_Rhino.Server.Application.Interfaces;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Services;

public sealed class RhinoBlockLifecycleService
{
    private readonly ILiveBlockLifecycleOperator _operator;

    public RhinoBlockLifecycleService(ILiveBlockLifecycleOperator blockOperator)
    {
        _operator = blockOperator;
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block definition item is required.");
        return validation.Success ? _operator.PreviewCreateDefinitions(filePath, specs) : OperationResponse<BlockMutationPreviewResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block definition item is required.");
        return validation.Success ? _operator.ApplyCreateDefinitions(filePath, specs) : OperationResponse<BlockMutationApplyResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance item is required.");
        return validation.Success ? _operator.PreviewInsertInstances(filePath, specs, attributes) : OperationResponse<BlockMutationPreviewResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance item is required.");
        return validation.Success ? _operator.ApplyInsertInstances(filePath, specs, attributes) : OperationResponse<BlockMutationApplyResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance transform item is required.");
        return validation.Success ? _operator.PreviewTransformInstances(filePath, specs) : OperationResponse<BlockMutationPreviewResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance transform item is required.");
        return validation.Success ? _operator.ApplyTransformInstances(filePath, specs) : OperationResponse<BlockMutationApplyResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance explode item is required.");
        return validation.Success ? _operator.PreviewExplodeInstances(filePath, specs) : OperationResponse<BlockMutationPreviewResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs)
    {
        OperationResponse validation = ValidateNonEmpty(specs.Count, "At least one block instance explode item is required.");
        return validation.Success ? _operator.ApplyExplodeInstances(filePath, specs) : OperationResponse<BlockMutationApplyResponse>.Fail(validation.Message);
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec)
    {
        return _operator.PreviewPurgeUnusedDefinitions(filePath, spec);
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec)
    {
        return _operator.ApplyPurgeUnusedDefinitions(filePath, spec);
    }

    private static OperationResponse ValidateNonEmpty(int count, string message)
    {
        return count > 0 ? OperationResponse.Ok() : OperationResponse.Fail(message);
    }
}
