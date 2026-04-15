using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class UserAttributeConditionRequest
{
    public string Key { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public UserAttributeComparisonMode ComparisonMode { get; set; } = UserAttributeComparisonMode.Exact;
}