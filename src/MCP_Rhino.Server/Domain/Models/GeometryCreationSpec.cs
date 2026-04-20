using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryCreationSpec
{
    public GeometryPrimitiveKind Primitive { get; set; }
    public ArcConstructionMode ArcMode { get; set; } = ArcConstructionMode.ThreePoint;
    public SurfaceConstructionMode SurfaceMode { get; set; } = SurfaceConstructionMode.FourCorners;

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double MidX { get; set; }
    public double MidY { get; set; }
    public double MidZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }

    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double Radius { get; set; }
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; }
    public double StartAngleRadians { get; set; }
    public double EndAngleRadians { get; set; }

    public double Corner0X { get; set; }
    public double Corner0Y { get; set; }
    public double Corner0Z { get; set; }
    public double Corner1X { get; set; }
    public double Corner1Y { get; set; }
    public double Corner1Z { get; set; }
    public double Corner2X { get; set; }
    public double Corner2Y { get; set; }
    public double Corner2Z { get; set; }
    public double Corner3X { get; set; }
    public double Corner3Y { get; set; }
    public double Corner3Z { get; set; }

    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
    public double ULength { get; set; }
    public double VLength { get; set; }
}
