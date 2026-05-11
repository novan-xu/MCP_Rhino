using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Application.Interfaces;

public interface ILiveBlockLifecycleOperator
{
    OperationResponse<BlockMutationPreviewResponse> PreviewCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs);

    OperationResponse<BlockMutationApplyResponse> ApplyCreateDefinitions(
        string filePath,
        IReadOnlyList<BlockDefinitionCreationSpec> specs);

    OperationResponse<BlockMutationPreviewResponse> PreviewInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes);

    OperationResponse<BlockMutationApplyResponse> ApplyInsertInstances(
        string filePath,
        IReadOnlyList<BlockInstancePlacementSpec> specs,
        ArchitecturalObjectAttributesSpec attributes);

    OperationResponse<BlockMutationPreviewResponse> PreviewTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs);

    OperationResponse<BlockMutationApplyResponse> ApplyTransformInstances(
        string filePath,
        IReadOnlyList<BlockInstanceTransformSpec> specs);

    OperationResponse<BlockMutationPreviewResponse> PreviewExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs);

    OperationResponse<BlockMutationApplyResponse> ApplyExplodeInstances(
        string filePath,
        IReadOnlyList<BlockExplodeSpec> specs);

    OperationResponse<BlockMutationPreviewResponse> PreviewPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec);

    OperationResponse<BlockMutationApplyResponse> ApplyPurgeUnusedDefinitions(
        string filePath,
        BlockPurgeSpec spec);
}
