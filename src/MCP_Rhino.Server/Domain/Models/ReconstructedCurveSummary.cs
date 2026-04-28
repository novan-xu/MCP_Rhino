namespace MCP_Rhino.Server.Domain.Models;

public sealed class ReconstructedCurveSummary
{
    public string CurveKind { get; set; } = string.Empty;
    public int Degree { get; set; }
    public bool IsClosed { get; set; }
    public bool IsValid { get; set; }
    public GeometryBoundingBoxData BoundingBox { get; set; } = new();
    public int PointCount { get; set; }
}
