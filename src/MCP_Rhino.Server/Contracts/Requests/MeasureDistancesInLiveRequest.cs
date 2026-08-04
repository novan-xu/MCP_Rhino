namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class MeasureDistancesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<MeasureDistanceEntryRequest> Entries { get; set; } = new();
}
