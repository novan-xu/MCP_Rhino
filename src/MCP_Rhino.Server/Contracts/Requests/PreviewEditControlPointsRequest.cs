namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class PreviewEditControlPointsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ControlPointEditEntryRequest> Entries { get; set; } = new();
}
