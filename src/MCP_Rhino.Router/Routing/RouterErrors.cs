using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace MCP_Rhino.Router.Routing;

public static class RouterErrors
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static CallToolResult Create(
        string code,
        string message,
        bool retryable = false,
        bool outcomeUnknown = false)
    {
        var payload = new RouterErrorPayload(code, message, retryable, outcomeUnknown);
        string json = JsonSerializer.Serialize(payload, SerializerOptions);
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = json }
            },
            StructuredContent = JsonSerializer.SerializeToElement(payload, SerializerOptions)
        };
    }

    private sealed record RouterErrorPayload(
        string Code,
        string Message,
        bool Retryable,
        bool OutcomeUnknown);
}
