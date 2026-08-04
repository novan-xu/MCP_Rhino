using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfacePointOrderPlan
{
    public Guid ObjectId { get; set; }
    public IReadOnlyList<OrderedSurfacePoint> Points { get; set; } = Array.Empty<OrderedSurfacePoint>();
    public SurfacePointOrderDirection Direction { get; set; }
    public int StartAnchorIndex { get; set; }
    public SurfaceReferenceCurveSpec ReferenceCurve { get; set; } = new();
    public SurfaceLocalCoordinateSystem LocalFrame { get; set; } = new();
    public SurfaceRebuildRouteKind Route { get; set; }
    public SurfaceTopologyKind Topology { get; set; } = SurfaceTopologyKind.Unknown;
    public bool PreserveBrepType { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
