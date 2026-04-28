extern alias rhinocommon;

using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Models;

public sealed class GeometryReplacementWithMetadata
{
    public Guid ObjectId { get; set; }
    public GeometryBase Geometry { get; set; } = null!;
}
