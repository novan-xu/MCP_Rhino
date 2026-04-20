namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class CreateSurfacesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<SurfaceItemRequest> Items { get; set; } = new();
    public GeometryCreationCommonOptions Common { get; set; } = new();
}
