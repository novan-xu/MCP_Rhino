using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class ControlPointEditSpec
{
    public Guid ObjectId { get; set; }
    public ControlPointTargetMode TargetMode { get; set; } = ControlPointTargetMode.CurveIndex;
    public int? PointIndex { get; set; }
    public int? UIndex { get; set; }
    public int? VIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double? Weight { get; set; }
}
