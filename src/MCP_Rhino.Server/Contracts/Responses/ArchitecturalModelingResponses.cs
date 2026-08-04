using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class ArchitecturalBoundingBoxResponse
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}

public sealed class ArchitecturalCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<ArchitecturalCreatedObjectResponse> CreatedObjects { get; set; } = Array.Empty<ArchitecturalCreatedObjectResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ArchitecturalCreatedObjectResponse
{
    public Guid ObjectId { get; set; }
    public ArchitecturalPrimitiveKind Kind { get; set; }
    public string Category { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public ArchitecturalBoundingBoxResponse? BoundingBox { get; set; }
}

public sealed class ArchitecturalBooleanPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public IReadOnlyList<ArchitecturalBooleanEntryResponse> Results { get; set; } = Array.Empty<ArchitecturalBooleanEntryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ArchitecturalBooleanApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public int DeletedCount { get; set; }
    public IReadOnlyList<ArchitecturalBooleanEntryResponse> Results { get; set; } = Array.Empty<ArchitecturalBooleanEntryResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class ArchitecturalBooleanEntryResponse
{
    public BooleanOperationKind Operation { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<Guid> ResultObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<ArchitecturalBoundingBoxResponse> ResultBoundingBoxes { get; set; } = Array.Empty<ArchitecturalBoundingBoxResponse>();
}

public sealed class BlockDefinitionMutationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public IReadOnlyList<BlockDefinitionResultResponse> Results { get; set; } = Array.Empty<BlockDefinitionResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockDefinitionResultResponse
{
    public string Name { get; set; } = string.Empty;
    public int DefinitionIndex { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class BlockInstanceCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public IReadOnlyList<BlockInstanceResultResponse> Results { get; set; } = Array.Empty<BlockInstanceResultResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class BlockInstanceResultResponse
{
    public Guid ObjectId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

