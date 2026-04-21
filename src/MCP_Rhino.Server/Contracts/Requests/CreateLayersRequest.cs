namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateLayersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<LayerCreationEntryRequest> Entries { get; set; } = new();
}
