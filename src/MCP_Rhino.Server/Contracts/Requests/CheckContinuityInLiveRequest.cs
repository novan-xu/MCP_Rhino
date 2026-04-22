namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CheckContinuityInLiveRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ContinuityCheckEntryRequest> Entries { get; set; } = new();
}
