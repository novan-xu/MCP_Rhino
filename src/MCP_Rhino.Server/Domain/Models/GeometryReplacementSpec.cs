namespace MCP_Rhino.Server.Domain.Models;

public sealed class GeometryReplacementSpec
{
    public Guid ObjectId { get; set; }
    public GeometryCreationSpec Geometry { get; set; } = new();
}
