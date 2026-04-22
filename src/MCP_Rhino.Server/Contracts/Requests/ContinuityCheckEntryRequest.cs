using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ContinuityCheckEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public Guid FirstObjectId { get; set; }
    public Guid SecondObjectId { get; set; }
    public int? FirstEdgeIndex { get; set; }
    public int? SecondEdgeIndex { get; set; }
    public GeometryContinuityKind TargetKind { get; set; } = GeometryContinuityKind.G2;
    public double PositionTolerance { get; set; } = 1e-6;
    public double AngleToleranceRadians { get; set; } = Math.PI / 180d;
    public double CurvatureToleranceRatio { get; set; } = 0.05d;
}
