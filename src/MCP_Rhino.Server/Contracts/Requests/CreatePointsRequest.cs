namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreatePointsRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<PointItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}
