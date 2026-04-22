namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class MeasureAnglesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<MeasureAngleEntryRequest> Entries { get; set; } = new();
}
