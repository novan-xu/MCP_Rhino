extern alias rhinocommon;

using GeometryBase = rhinocommon::Rhino.Geometry.GeometryBase;

namespace MCP_Rhino.Server.Application.Models;

public sealed class ResolvedGeometryReference
{
    public Guid ObjectId { get; set; }
    public string GeometryTypeName { get; set; } = string.Empty;
    public GeometryBase Geometry { get; set; } = null!;
    public double? Parameter { get; set; }
    public double? U { get; set; }
    public double? V { get; set; }
    public bool IsTemporary { get; set; }
}
