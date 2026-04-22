namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class MeasureDistanceEntryRequest
{
    public string EntryId { get; set; } = string.Empty;
    public GeometryAnalysisReferenceRequest From { get; set; } = new();
    public GeometryAnalysisReferenceRequest To { get; set; } = new();
    public double? Tolerance { get; set; }
}
