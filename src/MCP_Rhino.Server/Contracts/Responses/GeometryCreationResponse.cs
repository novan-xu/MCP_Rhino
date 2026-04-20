namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeometryCreationResponse
{
    public string FilePath { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CreatedCount { get; set; }
    public IReadOnlyList<GeometryCreatedObjectResponse> CreatedObjects { get; set; } = Array.Empty<GeometryCreatedObjectResponse>();
    public IReadOnlyList<ObjectEditWarning> Warnings { get; set; } = Array.Empty<ObjectEditWarning>();
}
