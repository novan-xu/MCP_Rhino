namespace MCP_Rhino.Server.Domain.Models;

public sealed class DerivedPointOperationParameters
{
    public double ScaleX { get; set; } = 1d;
    public double ScaleY { get; set; } = 1d;
    public double ScaleZ { get; set; } = 1d;
    public double VectorX { get; set; }
    public double VectorY { get; set; }
    public double VectorZ { get; set; }
    public double Distance { get; set; }
}
