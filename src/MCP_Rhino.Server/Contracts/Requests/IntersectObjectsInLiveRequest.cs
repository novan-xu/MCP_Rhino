namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class IntersectObjectsInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<GeometryIntersectionEntryRequest> Entries { get; set; } = new();
}
