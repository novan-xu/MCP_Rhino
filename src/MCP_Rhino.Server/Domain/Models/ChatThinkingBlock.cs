namespace MCP_Rhino.Server.Domain.Models;

public sealed record ChatThinkingBlock(
    string Text,
    DateTimeOffset CreatedAt);
