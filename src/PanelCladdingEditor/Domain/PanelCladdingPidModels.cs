namespace PanelCladdingEditor.Domain.Models.PanelCladding;

public sealed record PanelCladdingPidRequest(
    string ProjectCode,
    IReadOnlyList<Guid> PanelObjectIds,
    Guid NorthPanelObjectId,
    Guid FirstFloorPanelObjectId);

// The adapter validates the actual face's planarity, then supplies its oriented
// plane normal and four in-plane measurement corners of its outer boundary.
// Box thickness is not surface geometry. No Rhino geometry or temporary IDs enter planning.
public sealed record PanelCladdingPidPanel(
    Guid ObjectId,
    PanelPoint3 Normal,
    IReadOnlyList<PanelPoint3> Bounds,
    IReadOnlyDictionary<string, string> UserText);

public sealed record PanelCladdingPidAssignment(
    Guid ObjectId, string Elevation, string Level, string Bay, string Pid, string Cid)
{
    public IReadOnlyDictionary<string, string> UserTextWrites => new Dictionary<string, string>
    {
        ["CW_1.01_PID"] = Pid,
        ["CW_1.02_CID"] = Cid,
        ["CW_1.03_ELEVATION"] = Elevation,
        ["CW_1.04_LEVEL"] = Level
    };
}

// Panels contains selected writes only, even when the context contains the whole building.
public sealed record PanelCladdingPidPlan(IReadOnlyList<PanelCladdingPidAssignment> Panels)
{
    public int ContextPanelCount { get; init; }
}

public sealed record PanelCladdingPidExistingIdentity(
    Guid ObjectId, string Pid, string Cid, bool IsManagedDependency);

public sealed record PanelCladdingPidResult(PanelCladdingPidPlan Plan, int UpdatedPanelCount)
{
    public int ReorderedPanelCount { get; init; }
    public IReadOnlyList<string> PointOrderWarnings { get; init; } = Array.Empty<string>();
}
