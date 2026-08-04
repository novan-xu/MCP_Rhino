namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class LineItemRequest
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
}
