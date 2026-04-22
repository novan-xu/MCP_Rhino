namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetGeometryFramesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<GeometryFrameEntryRequest> Entries { get; set; } = new();
}
