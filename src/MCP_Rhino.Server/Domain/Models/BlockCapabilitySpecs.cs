using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class BlockPointSpec
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
}

public sealed class BlockDefinitionCreationSpec
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<Guid> SourceObjectIds { get; init; } = Array.Empty<Guid>();
    public BlockPointSpec BasePoint { get; init; } = new();
    public BlockSourceObjectPolicy SourceObjectPolicy { get; init; } = BlockSourceObjectPolicy.KeepVisible;
    public BlockDuplicateDefinitionPolicy DuplicateDefinitionPolicy { get; init; } = BlockDuplicateDefinitionPolicy.Reject;
}

public sealed class BlockInstancePlacementSpec
{
    public string DefinitionName { get; init; } = string.Empty;
    public BlockPointSpec Origin { get; init; } = new();
    public double RotationDegrees { get; init; }
    public double Scale { get; init; } = 1d;
}

public sealed class BlockInstanceTransformSpec
{
    public Guid ObjectId { get; init; }
    public double TranslationX { get; init; }
    public double TranslationY { get; init; }
    public double TranslationZ { get; init; }
    public double RotationDegrees { get; init; }
    public double Scale { get; init; } = 1d;
}

public sealed class BlockExplodeSpec
{
    public Guid ObjectId { get; init; }
    public bool ExplodeNestedInstances { get; init; }
    public bool SkipHiddenPieces { get; init; } = true;
}

public sealed class BlockPurgeSpec
{
    public IReadOnlyList<string> DefinitionNames { get; init; } = Array.Empty<string>();
    public bool IncludeAllUnused { get; init; } = true;
    public BlockPurgePolicy Policy { get; init; } = BlockPurgePolicy.UnusedLocalOnly;
}
