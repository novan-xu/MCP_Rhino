using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceRebuildDescriptor
{
    public Guid ObjectId { get; set; }
    public string GeometryKind { get; set; } = string.Empty;
    public IReadOnlyList<SurfaceReferenceCurveSpec> CandidateReferenceCurves { get; set; } = Array.Empty<SurfaceReferenceCurveSpec>();
    public SurfaceReferenceCurveSpec? SuggestedReferenceCurve { get; set; }
    public SurfaceBoundaryLoop OuterBoundaryLoop { get; set; } = new();
    public SurfaceLocalCoordinateSystem LocalFrame { get; set; } = new();
    public SurfacePointOrderDirection SuggestedDirection { get; set; } = SurfacePointOrderDirection.CounterClockwise;
    public SurfacePointOrderStartAnchorMode SuggestedStartAnchor { get; set; } = SurfacePointOrderStartAnchorMode.ReferenceStart;
    public SurfaceRebuildRouteKind SuggestedRoute { get; set; } = SurfaceRebuildRouteKind.FourPoint;
    public SurfaceTopologyKind Topology { get; set; } = SurfaceTopologyKind.Unknown;
    public SurfaceReferenceAmbiguity Ambiguity { get; set; } = new();
    public bool PreserveBrepType { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class SurfaceReferenceAmbiguity
{
    public string Level { get; set; } = "None";
    public string Reason { get; set; } = string.Empty;
}
