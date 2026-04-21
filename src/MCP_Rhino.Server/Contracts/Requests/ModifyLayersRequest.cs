namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ModifyLayersRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<LayerModificationEntryRequest> Entries { get; set; } = new();
}
