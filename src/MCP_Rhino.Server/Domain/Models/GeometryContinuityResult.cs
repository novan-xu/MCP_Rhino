using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryContinuityResult
{
    public string EntryId { get; set; } = string.Empty;
    public Guid FirstObjectId { get; set; }
    public Guid SecondObjectId { get; set; }
    public GeometryContinuityKind TargetKind { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsContinuous { get; set; }
    public double? GapDistance { get; set; }
    public double? TangentAngleRadians { get; set; }
    public double? CurvatureDifferenceRatio { get; set; }
}
