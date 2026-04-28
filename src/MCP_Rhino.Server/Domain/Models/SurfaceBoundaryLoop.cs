using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceBoundaryLoop
{
    public IReadOnlyList<GeometryPointData> Vertices3d { get; set; } = Array.Empty<GeometryPointData>();
    public bool IsClosed { get; set; }
    public bool IsPlanar { get; set; }
    public bool HasInnerLoops { get; set; }
    public SurfaceTopologyKind Topology { get; set; } = SurfaceTopologyKind.Unknown;
    public double Tolerance { get; set; }
}
