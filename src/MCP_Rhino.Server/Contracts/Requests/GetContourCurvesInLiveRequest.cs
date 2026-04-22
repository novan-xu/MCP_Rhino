namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetContourCurvesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<GeometryContourEntryRequest> Entries { get; set; } = new();
}
