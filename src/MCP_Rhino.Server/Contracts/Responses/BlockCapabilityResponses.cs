using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class BlockBoundingBoxResponse
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

public sealed class BlockTransformResponse
{
    public IReadOnlyList<double> Matrix { get; set; } = Array.Empty<double>();
    public double TranslationX { get; set; }
    public double TranslationY { get; set; }
    public double TranslationZ { get; set; }
    public double EstimatedScaleX { get; set; }
    public double EstimatedScaleY { get; set; }
    public double EstimatedScaleZ { get; set; }
    public double EstimatedRotationZDegrees { get; set; }
}

public sealed class BlockDefinitionListResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int DefinitionCount { get; set; }
    public IReadOnlyList<BlockDefinitionSummaryResponse> Definitions { get; set; } = Array.Empty<BlockDefinitionSummaryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockDefinitionDetailResponse
{
    public string FilePath { get; set; } = string.Empty;
    public BlockDefinitionSummaryResponse Definition { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockDefinitionSummaryResponse
{
    public Guid DefinitionId { get; set; }
    public int DefinitionIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BlockLinkStatus LinkStatus { get; set; } = BlockLinkStatus.Unknown;
    public string UpdateType { get; set; } = string.Empty;
    public string SourceArchive { get; set; } = string.Empty;
    public string ArchiveFileStatus { get; set; } = string.Empty;
    public bool IsReference { get; set; }
    public int ObjectCount { get; set; }
    public int TopLevelInstanceCount { get; set; }
    public int NestedInstanceCount { get; set; }
    public IReadOnlyList<string> NestedDefinitionNames { get; set; } = Array.Empty<string>();
    public BlockBoundingBoxResponse? BoundingBox { get; set; }
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BlockInstanceListResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public IReadOnlyList<BlockInstanceSummaryResponse> Instances { get; set; } = Array.Empty<BlockInstanceSummaryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockInstanceDetailResponse
{
    public string FilePath { get; set; } = string.Empty;
    public BlockInstanceSummaryResponse Instance { get; set; } = new();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockInstanceSummaryResponse
{
    public Guid ObjectId { get; set; }
    public Guid DefinitionId { get; set; }
    public int DefinitionIndex { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public BlockLinkStatus DefinitionLinkStatus { get; set; } = BlockLinkStatus.Unknown;
    public string LayerFullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public string ColorSource { get; set; } = string.Empty;
    public BlockTransformResponse Transform { get; set; } = new();
    public BlockBoundingBoxResponse? BoundingBox { get; set; }
    public Dictionary<string, string> UserText { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BlockMutationPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int MatchedCount { get; set; }
    public int WouldChangeCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<BlockOperationPreviewItemResponse> Results { get; set; } = Array.Empty<BlockOperationPreviewItemResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockMutationApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int ChangedCount { get; set; }
    public int CreatedCount { get; set; }
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<BlockOperationResultResponse> Results { get; set; } = Array.Empty<BlockOperationResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockOperationPreviewItemResponse
{
    public Guid TargetObjectId { get; set; }
    public Guid DefinitionId { get; set; }
    public int DefinitionIndex { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BlockImpactResponse Impact { get; set; } = new();
}

public sealed class BlockOperationResultResponse
{
    public Guid TargetObjectId { get; set; }
    public Guid DefinitionId { get; set; }
    public int DefinitionIndex { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public IReadOnlyList<Guid> CreatedObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> DeletedObjectIds { get; set; } = Array.Empty<Guid>();
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class BlockImpactResponse
{
    public int ObjectCount { get; set; }
    public int TopLevelInstanceCount { get; set; }
    public int NestedInstanceCount { get; set; }
    public int SourceObjectCount { get; set; }
    public int CreatedObjectCount { get; set; }
    public int DeletedObjectCount { get; set; }
    public IReadOnlyList<string> NestedDefinitionNames { get; set; } = Array.Empty<string>();
}
