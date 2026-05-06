using System.Text.Json;

namespace MCP_Rhino.Companion;

public sealed record ClaudeCodeMcpServerStatus(string Name, string Status);

public sealed record ClaudeCodeStreamEvent(
    string Type,
    string RawJson,
    string? SessionId = null,
    string? Model = null,
    IReadOnlyList<ClaudeCodeMcpServerStatus>? McpServers = null,
    string? Text = null,
    string? Thinking = null,
    string? ToolUseId = null,
    string? ToolName = null,
    string? ToolInputSummary = null,
    string? ToolResultSummary = null,
    bool ToolResultIsError = false,
    decimal? TotalCostUsd = null,
    string? TerminalReason = null)
{
    public static bool TryParse(string line, out ClaudeCodeStreamEvent streamEvent)
    {
        streamEvent = new ClaudeCodeStreamEvent("unknown", line);

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            string type = ReadString(root, "type") ?? "unknown";
            string? subtype = ReadString(root, "subtype");
            string eventType = subtype is null ? type : $"{type}/{subtype}";

            List<ClaudeCodeMcpServerStatus> servers = ReadMcpServers(root);
            ContentReadResult content = ReadContent(root);

            streamEvent = new ClaudeCodeStreamEvent(
                eventType,
                line,
                ReadString(root, "session_id"),
                ReadString(root, "model"),
                servers,
                content.Text,
                content.Thinking,
                content.ToolUseId,
                content.ToolName,
                content.ToolInputSummary,
                content.ToolResultSummary,
                content.ToolResultIsError,
                ReadDecimal(root, "total_cost_usd"),
                ReadString(root, "terminal_reason"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<ClaudeCodeMcpServerStatus> ReadMcpServers(JsonElement root)
    {
        var servers = new List<ClaudeCodeMcpServerStatus>();
        if (!root.TryGetProperty("mcp_servers", out JsonElement mcpServers)
            || mcpServers.ValueKind != JsonValueKind.Array)
        {
            return servers;
        }

        foreach (JsonElement server in mcpServers.EnumerateArray())
        {
            string name = ReadString(server, "name") ?? "unknown";
            string status = ReadString(server, "status") ?? "unknown";
            servers.Add(new ClaudeCodeMcpServerStatus(name, status));
        }

        return servers;
    }

    private static ContentReadResult ReadContent(JsonElement root)
    {
        if (!root.TryGetProperty("message", out JsonElement message)
            || !message.TryGetProperty("content", out JsonElement content)
            || content.ValueKind != JsonValueKind.Array)
        {
            return new ContentReadResult();
        }

        var result = new ContentReadResult();
        foreach (JsonElement block in content.EnumerateArray())
        {
            string? blockType = ReadString(block, "type");
            switch (blockType)
            {
                case "text":
                    result.Text = AppendText(result.Text, ReadString(block, "text"));
                    break;
                case "thinking":
                    result.Thinking = AppendText(result.Thinking, ReadString(block, "thinking"));
                    result.Thinking = AppendText(result.Thinking, ReadString(block, "text"));
                    break;
                case "tool_use":
                    result.ToolUseId = ReadString(block, "id");
                    result.ToolName = ReadString(block, "name");
                    result.ToolInputSummary = block.TryGetProperty("input", out JsonElement input)
                        ? input.GetRawText()
                        : null;
                    break;
                case "tool_result":
                    result.ToolUseId = ReadString(block, "tool_use_id");
                    result.ToolResultSummary = block.TryGetProperty("content", out JsonElement resultContent)
                        ? FormatResultContent(resultContent)
                        : null;
                    result.ToolResultIsError = block.TryGetProperty("is_error", out JsonElement isError)
                        && isError.ValueKind is JsonValueKind.True;
                    break;
            }
        }

        return result;
    }

    private static string FormatResultContent(JsonElement content)
    {
        return content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? string.Empty
            : content.GetRawText();
    }

    private static string? AppendText(string? existing, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return existing;
        }

        return string.IsNullOrWhiteSpace(existing)
            ? next
            : existing + Environment.NewLine + next;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetDecimal(out decimal value) ? value : null;
    }

    private sealed class ContentReadResult
    {
        public string? Text { get; set; }
        public string? Thinking { get; set; }
        public string? ToolUseId { get; set; }
        public string? ToolName { get; set; }
        public string? ToolInputSummary { get; set; }
        public string? ToolResultSummary { get; set; }
        public bool ToolResultIsError { get; set; }
    }
}
