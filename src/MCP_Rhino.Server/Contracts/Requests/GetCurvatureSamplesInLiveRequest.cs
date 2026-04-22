namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GetCurvatureSamplesInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<CurvatureSampleEntryRequest> Entries { get; set; } = new();
}
