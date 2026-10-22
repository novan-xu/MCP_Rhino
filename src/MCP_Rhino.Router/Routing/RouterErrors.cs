using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace MCP_Rhino.Router.Routing;

public static class RouterErrors
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static CallToolResult Create(
        string code,
        string message,
        bool retryable = false,
        bool outcomeUnknown = false,
        string? diagnostic = null)
    {
        var payload = new RouterErrorPayload(code, message, retryable, outcomeUnknown, diagnostic);
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

    // Maps a document's own route diagnostic to the error an agent should act on.
    public static CallToolResult ForDocumentDiagnostic(string diagnostic)
    {
        return diagnostic switch
        {
            BackendConnectException.EndpointBusy => Create(
                "DOCUMENT_ENDPOINT_BUSY",
                "The Rhino document is open, but other agent sessions hold every connection slot of its route. Retry shortly, or ask the user to end idle agent sessions to free slots. Offline tools are not a substitute for the open document.",
                retryable: true,
                diagnostic: diagnostic),
            BackendConnectException.EndpointNotListening => Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                "The document is published, but its route listener is not running.",
                retryable: true,
                diagnostic: diagnostic),
            BackendConnectException.AttestationMismatch => Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                "The document endpoint failed private attestation against its published descriptor.",
                retryable: true,
                diagnostic: diagnostic),
            _ => Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                $"The requested document session is not routable ({diagnostic}).",
                retryable: true,
                diagnostic: diagnostic)
        };
    }

    private sealed record RouterErrorPayload(
        string Code,
        string Message,
        bool Retryable,
        bool OutcomeUnknown,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Diagnostic);
}
