using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeometryEditPreviewResponse
{
    public string FilePath { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public GeometryEditStrategyKind Strategy { get; set; }
    public EditableCurveStructure? DescriptorSummary { get; set; }
    public EditableSurfaceStructure? SurfaceDescriptorSummary { get; set; }
    public int DescriptorPointCount { get; set; }
    public ReconstructedCurveSummary? ReconstructedCurveSummary { get; set; } = new();
    public SurfaceControlPointGridSnapshot? SurfaceControlPointGridSnapshot { get; set; }
    public IReadOnlyList<int> ResolvedPointIndices { get; set; } = Array.Empty<int>();
    public DerivedOperationApplied? DerivedOperationApplied { get; set; }
    public StrategyResolutionTrace? StrategyResolutionTrace { get; set; }
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
