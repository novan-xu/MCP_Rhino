using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryTransformSpec
{
    public GeometryTransformKind Kind { get; set; } = GeometryTransformKind.Translate;
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; }
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisZ { get; set; }
    public double AngleRadians { get; set; }
    public double ScaleFactor { get; set; }
}
