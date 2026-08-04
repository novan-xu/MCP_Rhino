namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateArcsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<ArcItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}
