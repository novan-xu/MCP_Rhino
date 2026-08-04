namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetClosestPointsInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<GeometryClosestPointEntryRequest> Entries { get; set; } = new();
}
