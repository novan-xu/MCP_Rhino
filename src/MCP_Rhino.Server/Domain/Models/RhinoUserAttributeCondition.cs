using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed class RhinoUserAttributeCondition
{
    public string Key { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public UserAttributeComparisonMode ComparisonMode { get; set; } = UserAttributeComparisonMode.Exact;
}