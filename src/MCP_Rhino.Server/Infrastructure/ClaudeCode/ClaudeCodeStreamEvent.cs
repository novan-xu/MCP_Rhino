using System.Text.Json;

namespace MCP_Rhino.Server.Infrastructure.ClaudeCode;

public sealed record ClaudeCodeMcpServerStatus(string Name, string Status);

public sealed record ClaudeCodeStreamEvent(
    string Type,
    string RawJson,
    string? SessionId = null,
    string? Model = null,
    IReadOnlyList<ClaudeCodeMcpServerStatus>? McpServers = null,
    string? Text = null,
    string? ToolUseId = null,
    string? ToolName = null,
    string? ToolInputSummary = null,
    string? ToolResultSummary = null,
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
            (string? text, string? toolUseId, string? toolName, string? toolInputSummary, string? toolResultSummary) =
                ReadContent(root);

            streamEvent = new ClaudeCodeStreamEvent(
                eventType,
                line,
                ReadString(root, "session_id"),
                ReadString(root, "model"),
                servers,
                text,
                toolUseId,
                toolName,
                toolInputSummary,
                toolResultSummary,
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

    private static (string? Text, string? ToolUseId, string? ToolName, string? ToolInputSummary, string? ToolResultSummary)
        ReadContent(JsonElement root)
    {
        if (!root.TryGetProperty("message", out JsonElement message)
            || !message.TryGetProperty("content", out JsonElement content)
            || content.ValueKind != JsonValueKind.Array)
        {
            return (null, null, null, null, null);
        }

        string? text = null;
        string? toolUseId = null;
        string? toolName = null;
        string? toolInputSummary = null;
        string? toolResultSummary = null;

        foreach (JsonElement block in content.EnumerateArray())
        {
            string? blockType = ReadString(block, "type");
            switch (blockType)
            {
                case "text":
                case "thinking":
                    text = AppendText(text, ReadString(block, blockType));
                    text = AppendText(text, ReadString(block, "text"));
                    break;
                case "tool_use":
                    toolUseId = ReadString(block, "id");
                    toolName = ReadString(block, "name");
                    toolInputSummary = block.TryGetProperty("input", out JsonElement input)
                        ? input.GetRawText()
                        : null;
                    break;
                case "tool_result":
                    toolUseId = ReadString(block, "tool_use_id");
                    toolResultSummary = block.TryGetProperty("content", out JsonElement resultContent)
                        ? resultContent.GetRawText()
                        : null;
                    break;
            }
        }

        return (text, toolUseId, toolName, toolInputSummary, toolResultSummary);
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
}
