using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class BlockPointRequest
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ListBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public bool IncludeLinked { get; set; } = true;
    public bool IncludeNestedSummary { get; set; } = true;
}

public sealed class GetBlockDefinitionDetailsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string DefinitionName { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public bool IncludeNestedSummary { get; set; } = true;
}

public sealed class ListBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string DefinitionName { get; set; } = string.Empty;
    public bool IncludeHidden { get; set; }
}

public sealed class GetBlockInstanceDetailsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
}

public sealed class PreviewCreateBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockDefinitionSourceItemRequest> Items { get; set; } = new();
}

public sealed class ApplyCreateBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockDefinitionSourceItemRequest> Items { get; set; } = new();
}

public sealed class BlockDefinitionSourceItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Guid> SourceObjectIds { get; set; } = new();
    public BlockPointRequest BasePoint { get; set; } = new();
    public BlockSourceObjectPolicy SourceObjectPolicy { get; set; } = BlockSourceObjectPolicy.KeepVisible;
    public BlockDuplicateDefinitionPolicy DuplicateDefinitionPolicy { get; set; } = BlockDuplicateDefinitionPolicy.Reject;
}

public sealed class PreviewInsertBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockInstancePlacementRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class ApplyInsertBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockInstancePlacementRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
    public bool AutoCreateLayers { get; set; }
}

public sealed class BlockInstancePlacementRequest
{
    public string DefinitionName { get; set; } = string.Empty;
    public BlockPointRequest Origin { get; set; } = new();
    public double RotationDegrees { get; set; }
    public double Scale { get; set; } = 1d;
}

public sealed class PreviewTransformBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockInstanceTransformRequest> Items { get; set; } = new();
}

public sealed class ApplyTransformBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockInstanceTransformRequest> Items { get; set; } = new();
}

public sealed class BlockInstanceTransformRequest
{
    public Guid ObjectId { get; set; }
    public double TranslationX { get; set; }
    public double TranslationY { get; set; }
    public double TranslationZ { get; set; }
    public double RotationDegrees { get; set; }
    public double Scale { get; set; } = 1d;
}

public sealed class PreviewExplodeBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockExplodeInstanceRequest> Items { get; set; } = new();
}

public sealed class ApplyExplodeBlockInstancesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<BlockExplodeInstanceRequest> Items { get; set; } = new();
}

public sealed class BlockExplodeInstanceRequest
{
    public Guid ObjectId { get; set; }
    public bool ExplodeNestedInstances { get; set; }
    public bool SkipHiddenPieces { get; set; } = true;
}

public sealed class PreviewPurgeUnusedBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> DefinitionNames { get; set; } = new();
    public bool IncludeAllUnused { get; set; } = true;
    public BlockPurgePolicy Policy { get; set; } = BlockPurgePolicy.UnusedLocalOnly;
}

public sealed class ApplyPurgeUnusedBlockDefinitionsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> DefinitionNames { get; set; } = new();
    public bool IncludeAllUnused { get; set; } = true;
    public BlockPurgePolicy Policy { get; set; } = BlockPurgePolicy.UnusedLocalOnly;
}
