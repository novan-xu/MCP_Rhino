using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceRebuildSpec
{
    public SurfacePointOrderGuideMode GuideMode { get; set; } = SurfacePointOrderGuideMode.Auto;
    public SurfacePointOrderDirection Direction { get; set; } = SurfacePointOrderDirection.CounterClockwise;
    public SurfacePointOrderStartAnchorMode StartAnchorMode { get; set; } = SurfacePointOrderStartAnchorMode.ReferenceStart;
    public Guid? ReferenceCurveObjectId { get; set; }
    public int? ReferenceEdgeIndex { get; set; }
}
