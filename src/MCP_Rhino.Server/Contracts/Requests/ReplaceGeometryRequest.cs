namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class ReplaceGeometryRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<GeometryReplacementEntryRequest> Entries { get; set; } = new();
}
