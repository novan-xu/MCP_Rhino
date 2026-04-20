namespace MCP_Rhino.Server.Contracts.Responses;

public sealed class DocumentUserStringEntryResponse
{
    public string? Section { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
