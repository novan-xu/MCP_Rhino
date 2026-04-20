using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class SurfaceItemRequest
{
    public SurfaceConstructionMode Mode { get; set; } = SurfaceConstructionMode.FourCorners;
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
    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; } = 1d;
    public double ULength { get; set; }
    public double VLength { get; set; }
}
