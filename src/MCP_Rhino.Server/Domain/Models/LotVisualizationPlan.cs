using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class LotVisualizationSpec
{
    public IReadOnlyList<Guid> ObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<string> LotNumberKeys { get; set; } = Array.Empty<string>();
    public IReadOnlySet<string> IneffectiveLotValues { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string GroupPrefix { get; set; } = "MCP_Lot_";
}

public sealed class LotVisualizationPlan
{
    public IReadOnlyList<Guid> TargetObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<string> LayerFullPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ResolvedLotNumberKeys { get; set; } = Array.Empty<string>();
    public IReadOnlyList<LotVisualizationLotPlan> Lots { get; set; } = Array.Empty<LotVisualizationLotPlan>();
    public IReadOnlyList<Guid> UnassignedObjectIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}

public sealed class LotVisualizationLotPlan
{
    public string LotNumber { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public RhinoDisplayColor Color { get; set; } = new();
    public IReadOnlyList<Guid> ObjectIds { get; set; } = Array.Empty<Guid>();
}

public sealed class LotVisualizationApplyResult
{
    public LotVisualizationPlan Plan { get; set; } = new();
    public int ModifiedObjectCount { get; set; }
    public int CreatedGroupCount { get; set; }
    public int ReusedGroupCount { get; set; }
}
