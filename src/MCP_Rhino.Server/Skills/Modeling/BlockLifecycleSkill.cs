using MCP_Rhino.Server.Application.Services;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Skills.Modeling;

public sealed class BlockLifecycleSkill
{
    private readonly RhinoBlockLifecycleService _service;

    public BlockLifecycleSkill(RhinoBlockLifecycleService service)
    {
        _service = service;
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewCreate(PreviewCreateBlockDefinitionsRequest request)
    {
        return _service.PreviewCreateDefinitions(request.FilePath, MapDefinitionItems(request.Items));
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyCreate(ApplyCreateBlockDefinitionsRequest request)
    {
        return _service.ApplyCreateDefinitions(request.FilePath, MapDefinitionItems(request.Items));
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewInsert(PreviewInsertBlockInstancesRequest request)
    {
        return _service.PreviewInsertInstances(
            request.FilePath,
            MapPlacementItems(request.Items),
            ArchitecturalPrimitiveCreationSkill.MapAttributes(request.Common, null, request.AutoCreateLayers));
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyInsert(ApplyInsertBlockInstancesRequest request)
    {
        return _service.ApplyInsertInstances(
            request.FilePath,
            MapPlacementItems(request.Items),
            ArchitecturalPrimitiveCreationSkill.MapAttributes(request.Common, null, request.AutoCreateLayers));
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewTransform(PreviewTransformBlockInstancesRequest request)
    {
        return _service.PreviewTransformInstances(request.FilePath, MapTransformItems(request.Items));
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyTransform(ApplyTransformBlockInstancesRequest request)
    {
        return _service.ApplyTransformInstances(request.FilePath, MapTransformItems(request.Items));
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewExplode(PreviewExplodeBlockInstancesRequest request)
    {
        return _service.PreviewExplodeInstances(request.FilePath, MapExplodeItems(request.Items));
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyExplode(ApplyExplodeBlockInstancesRequest request)
    {
        return _service.ApplyExplodeInstances(request.FilePath, MapExplodeItems(request.Items));
    }

    public OperationResponse<BlockMutationPreviewResponse> PreviewPurge(PreviewPurgeUnusedBlockDefinitionsRequest request)
    {
        return _service.PreviewPurgeUnusedDefinitions(request.FilePath, new BlockPurgeSpec
        {
            DefinitionNames = request.DefinitionNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList(),
            IncludeAllUnused = request.IncludeAllUnused,
            Policy = request.Policy
        });
    }

    public OperationResponse<BlockMutationApplyResponse> ApplyPurge(ApplyPurgeUnusedBlockDefinitionsRequest request)
    {
        return _service.ApplyPurgeUnusedDefinitions(request.FilePath, new BlockPurgeSpec
        {
            DefinitionNames = request.DefinitionNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList(),
            IncludeAllUnused = request.IncludeAllUnused,
            Policy = request.Policy
        });
    }

    private static IReadOnlyList<BlockDefinitionCreationSpec> MapDefinitionItems(IEnumerable<BlockDefinitionSourceItemRequest>? items)
    {
        return (items ?? Array.Empty<BlockDefinitionSourceItemRequest>())
            .Select(item => new BlockDefinitionCreationSpec
            {
                Name = item.Name,
                Description = item.Description,
                SourceObjectIds = item.SourceObjectIds ?? new List<Guid>(),
                BasePoint = new BlockPointSpec
                {
                    X = item.BasePoint?.X ?? 0d,
                    Y = item.BasePoint?.Y ?? 0d,
                    Z = item.BasePoint?.Z ?? 0d
                },
                SourceObjectPolicy = item.SourceObjectPolicy,
                DuplicateDefinitionPolicy = item.DuplicateDefinitionPolicy
            })
            .ToList();
    }

    private static IReadOnlyList<BlockInstancePlacementSpec> MapPlacementItems(IEnumerable<BlockInstancePlacementRequest>? items)
    {
        return (items ?? Array.Empty<BlockInstancePlacementRequest>())
            .Select(item => new BlockInstancePlacementSpec
            {
                DefinitionName = item.DefinitionName,
                Origin = new BlockPointSpec
                {
                    X = item.Origin?.X ?? 0d,
                    Y = item.Origin?.Y ?? 0d,
                    Z = item.Origin?.Z ?? 0d
                },
                RotationDegrees = item.RotationDegrees,
                Scale = item.Scale
            })
            .ToList();
    }

    private static IReadOnlyList<BlockInstanceTransformSpec> MapTransformItems(IEnumerable<BlockInstanceTransformRequest>? items)
    {
        return (items ?? Array.Empty<BlockInstanceTransformRequest>())
            .Select(item => new BlockInstanceTransformSpec
            {
                ObjectId = item.ObjectId,
                TranslationX = item.TranslationX,
                TranslationY = item.TranslationY,
                TranslationZ = item.TranslationZ,
                RotationDegrees = item.RotationDegrees,
                Scale = item.Scale
            })
            .ToList();
    }

    private static IReadOnlyList<BlockExplodeSpec> MapExplodeItems(IEnumerable<BlockExplodeInstanceRequest>? items)
    {
        return (items ?? Array.Empty<BlockExplodeInstanceRequest>())
            .Select(item => new BlockExplodeSpec
            {
                ObjectId = item.ObjectId,
                ExplodeNestedInstances = item.ExplodeNestedInstances,
                SkipHiddenPieces = item.SkipHiddenPieces
            })
            .ToList();
    }
}
