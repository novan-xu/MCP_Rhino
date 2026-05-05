namespace MCP_Rhino.Server.Domain.Models;

public sealed class SurfaceReferenceCurveSpec
{
    public Guid? ObjectId { get; set; }
    public int? EdgeIndex { get; set; }
    public int? StartVertexIndex { get; set; }
    public int? EndVertexIndex { get; set; }
    public bool ReversedFromBoundary { get; set; }
    public GeometryPointData StartPoint { get; set; } = new();
    public GeometryPointData EndPoint { get; set; } = new();
    public GeometryPointData Midpoint { get; set; } = new();
    public double Length { get; set; }
    public double Score { get; set; }
}
