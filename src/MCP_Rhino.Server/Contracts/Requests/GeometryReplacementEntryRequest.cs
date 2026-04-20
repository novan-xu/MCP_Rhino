using MCP_Rhino.Server.Domain.Models;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class GeometryReplacementEntryRequest
{
    public Guid ObjectId { get; set; }
    public GeometryCreationSpec Geometry { get; set; } = new();
}
