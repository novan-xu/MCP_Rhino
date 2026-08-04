namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateLinesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<LineItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}
