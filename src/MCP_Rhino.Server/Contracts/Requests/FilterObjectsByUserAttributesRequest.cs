using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Contracts.Requests;

public sealed class FilterObjectsByUserAttributesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public List<UserAttributeConditionRequest> Conditions { get; set; } = new();
    public FilterMatchMode MatchMode { get; set; } = FilterMatchMode.All;
}