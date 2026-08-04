namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class FilterObjectsByTypeRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> ObjectTypes { get; set; } = new();
}