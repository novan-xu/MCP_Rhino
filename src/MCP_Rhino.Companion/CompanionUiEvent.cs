using System.IO;

namespace MCP_Rhino.Companion;

public sealed record CompanionUiEvent(
    string Type,
    string? Role = null,
    string? Text = null,
    string? Status = null,
    string? ToolUseId = null,
    string? ToolName = null,
    string? ToolInput = null,
    string? ToolResult = null,
    string? DocumentPath = null,
    string? DocumentName = null,
    uint? RuntimeSerial = null,
    string? PipeName = null,
    string? Model = null,
    string? Cli = null,
    IReadOnlyList<string>? Models = null,
    decimal? TotalCostUsd = null,
    bool? InputEnabled = null)
{
    public static CompanionUiEvent Session(
        CompanionOptions options,
        string? cli = null,
        IReadOnlyList<string>? models = null)
    {
        return new CompanionUiEvent(
            "session",
            DocumentPath: options.DocumentPath,
            DocumentName: Path.GetFileName(options.DocumentPath),
            RuntimeSerial: options.RuntimeSerial,
            PipeName: options.PipeName,
            Model: options.ModelId,
            Cli: cli,
            Models: models);
    }

    public static CompanionUiEvent Message(string role, string text)
    {
        return new CompanionUiEvent("message", Role: role, Text: text);
    }

    public static CompanionUiEvent Thinking(string text)
    {
        return new CompanionUiEvent("thinking", Text: text);
    }

    public static CompanionUiEvent Diagnostic(string text)
    {
        return new CompanionUiEvent("diagnostic", Text: text);
    }

    public static CompanionUiEvent SessionStatus(string status)
    {
        return new CompanionUiEvent("status", Status: status);
    }

    public static CompanionUiEvent Input(bool enabled)
    {
        return new CompanionUiEvent("input", InputEnabled: enabled);
    }

    public static CompanionUiEvent Tool(
        string status,
        string? toolUseId,
        string? toolName,
        string? input,
        string? result)
    {
        return new CompanionUiEvent(
            "tool",
            Status: status,
            ToolUseId: toolUseId,
            ToolName: toolName,
            ToolInput: input,
            ToolResult: result);
    }

    public static CompanionUiEvent Result(decimal? totalCostUsd)
    {
        return new CompanionUiEvent("result", TotalCostUsd: totalCostUsd);
    }
}
