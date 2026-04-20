using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class GeometryCreatedObjectResponse
{
    public Guid ObjectId { get; set; }
    public GeometryPrimitiveKind Primitive { get; set; }
    public string LayerFullPath { get; set; } = string.Empty;
}
