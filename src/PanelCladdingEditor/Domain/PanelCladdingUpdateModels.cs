namespace PanelCladdingEditor.Domain.Models.PanelCladding;

public enum PanelCladdingDependencyKind
{
    Surface,
    Curve
}

public sealed class PanelCladdingExpectedDependency
{
    public Guid SourcePanelObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public PanelCladdingDependencyKind Kind { get; init; }
}

public sealed class PanelCladdingExistingDependency
{
    public Guid ObjectId { get; init; }
    public string PanelId { get; init; } = string.Empty;
    public string Cid { get; init; } = string.Empty;
    public PanelCladdingDependencyKind Kind { get; init; }
}

public sealed class PanelCladdingDependencyUpdateAction
{
    public Guid ObjectId { get; init; }
    public PanelCladdingExpectedDependency Expected { get; init; } = new();
}

public sealed class PanelCladdingDependencyReconciliationPlan
{
    public IReadOnlyList<PanelCladdingExpectedDependency> Creates { get; init; } =
        Array.Empty<PanelCladdingExpectedDependency>();
    public IReadOnlyList<PanelCladdingDependencyUpdateAction> Updates { get; init; } =
        Array.Empty<PanelCladdingDependencyUpdateAction>();
    public IReadOnlyList<Guid> Deletes { get; init; } = Array.Empty<Guid>();
}

public sealed class PanelCladdingUpdateResult
{
    public IReadOnlyList<Guid> ReorderedPanelIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<string> PointOrderWarnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<Guid> SourcePanelIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<PanelCladdingDuplicateCidGroup> DuplicateCidGroups { get; init; } =
        Array.Empty<PanelCladdingDuplicateCidGroup>();
    public IReadOnlyList<Guid> PreservedAmbiguousDependencyIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedSurfaceIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> CreatedCurveIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> UpdatedSurfaceIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> UpdatedCurveIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> DeletedObjectIds { get; init; } = Array.Empty<Guid>();
}

public sealed record PanelCladdingUpdateSource(Guid ObjectId, string PanelId, string PanelCid);

public sealed record PanelCladdingDuplicateCidGroup(string Cid, IReadOnlyList<Guid> PanelObjectIds);

public sealed class PanelCladdingUpdateSelection
{
    public IReadOnlyList<PanelCladdingUpdateSource> ProcessableSources { get; init; } =
        Array.Empty<PanelCladdingUpdateSource>();
    public IReadOnlyList<PanelCladdingDuplicateCidGroup> DuplicateCidGroups { get; init; } =
        Array.Empty<PanelCladdingDuplicateCidGroup>();
}
