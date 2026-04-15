using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoObjectInfo
{
    public Guid ObjectId { get; set; }
    public string ObjectTypeName { get; set; } = string.Empty;
    public RhinoObjectType NormalizedObjectType { get; set; } = RhinoObjectType.Unknown;
    public string GeometryTypeName { get; set; } = string.Empty;
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string LayerFullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<RhinoObjectUserAttributeEntry> UserAttributes { get; set; } = Array.Empty<RhinoObjectUserAttributeEntry>();

    public string? GetUserAttributeValue(string key)
    {
        return UserAttributes.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}