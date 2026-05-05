using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed record ChatToolCall(
    string Id,
    string Name,
    string InputSummary,
    ChatToolCallStatus Status,
    string? ResultSummary,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
