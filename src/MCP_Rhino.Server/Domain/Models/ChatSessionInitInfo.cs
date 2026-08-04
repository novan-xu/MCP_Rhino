namespace MCP_Rhino.Server.Domain.Models;

public sealed record ChatSessionInitInfo(
    string? SessionId,
    string? Model,
    string WorkingDirectory,
    IReadOnlyList<string> McpServerStatuses);
