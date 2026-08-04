namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryContourEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double StartZ { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double EndZ { get; set; }
    public double Interval { get; set; } = 1d;
}
