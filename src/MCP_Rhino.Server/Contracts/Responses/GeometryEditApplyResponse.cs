using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeometryEditApplyResponse
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public GeometryEditStrategyKind Strategy { get; set; }
    public string UndoRecordName { get; set; } = string.Empty;
    public IReadOnlyList<string> MetadataDropped { get; set; } = Array.Empty<string>();
    public SurfaceControlPointGridSnapshot? SurfaceControlPointGridSnapshot { get; set; }
    public IReadOnlyList<int> ResolvedPointIndices { get; set; } = Array.Empty<int>();
    public DerivedOperationApplied? DerivedOperationApplied { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
