using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ArcItemRequest
{
    public ArcConstructionMode Mode { get; set; } = ArcConstructionMode.ThreePoint;
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
    public double NormalZ { get; set; } = 1d;
    public double StartAngleRadians { get; set; }
    public double EndAngleRadians { get; set; }
}
