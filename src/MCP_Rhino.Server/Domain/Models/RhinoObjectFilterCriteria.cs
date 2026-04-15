using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoObjectFilterCriteria
{
    public List<string> LayerQueries { get; set; } = new();
    public List<string> LayerFullPaths { get; set; } = new();
    public List<string> ObjectTypes { get; set; } = new();
    public List<RhinoUserAttributeCondition> UserAttributeConditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
    public FilterMatchMode UserAttributeMatchMode { get; set; } = FilterMatchMode.All;

    public bool HasAnyCriteria()
    {
        return LayerQueries.Count > 0
            || LayerFullPaths.Count > 0
            || ObjectTypes.Count > 0
            || UserAttributeConditions.Count > 0;
    }
}