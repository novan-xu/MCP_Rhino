using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Domain.Models;

public sealed record ChatMessage(
    Guid Id,
    ChatRole Role,
    string Text,
    DateTimeOffset CreatedAt);
