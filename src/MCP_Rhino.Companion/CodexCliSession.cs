using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MCP_Rhino.Companion;

public sealed class CodexCliSession : IAgentSession
{
    private const string McpServerName = "rhino";

    private readonly CompanionOptions _options;
    private readonly object _processLock = new();
    private readonly HashSet<string> _diagnosticOnceSeen = new(StringComparer.OrdinalIgnoreCase);
    // codex exec is one-shot per process, so we maintain conversation history
    // ourselves and prepend it to every turn's stdin prompt so the agent
    // retains context across turns.
    private readonly List<(string Role, string Text)> _history = new();
    // Buffer the assistant text emitted within the current turn so we can
    // commit it to _history when the turn completes.
    private readonly System.Text.StringBuilder _currentTurnAssistant = new();
    private const int HistoryCharBudget = 60_000;
    private Process? _currentTurnProcess;
    private bool _disposed;
    private bool _ready;
    private string? _executablePath;
    private string? _lastThreadId;

    public CodexCliSession(CompanionOptions options)
    {
        _options = options;
    }

    public event EventHandler<CompanionUiEvent>? EventReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Emit(CompanionUiEvent.SessionStatus("Codex starting"));
        Emit(CompanionUiEvent.Input(false));

        CodexCliAvailabilityResult availability = CodexCliAvailability.Check();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
        {
            Emit(CompanionUiEvent.Diagnostic(availability.Message));
            Emit(CompanionUiEvent.SessionStatus("Codex unavailable"));
            return Task.CompletedTask;
        }

        if (!File.Exists(_options.BridgePath))
        {
            Emit(CompanionUiEvent.Diagnostic("Bridge not found: " + _options.BridgePath));
            Emit(CompanionUiEvent.SessionStatus("Bridge missing"));
            return Task.CompletedTask;
        }

        _executablePath = availability.ExecutablePath;
        _ready = true;
        Emit(CompanionUiEvent.SessionStatus("Codex ready"));
        Emit(CompanionUiEvent.Input(true));
        return Task.CompletedTask;
    }

    public async Task SendUserMessageAsync(string text, CancellationToken cancellationToken)
    {
        if (!_ready || string.IsNullOrWhiteSpace(_executablePath))
        {
            Emit(CompanionUiEvent.Diagnostic("Codex CLI is not ready."));
            return;
        }

        Emit(CompanionUiEvent.Message("user", text));
        Emit(CompanionUiEvent.Input(false));
        Emit(CompanionUiEvent.SessionStatus("Codex working"));

        // Reset the assistant buffer for the new turn.
        _currentTurnAssistant.Clear();

        string workingDirectory = CompanionWorkspace.GetWorkingDirectory(_options.PipeName);
        string prompt = BuildPromptWithHistory(text);

        var arguments = new List<string>
        {
            "exec",
            "--json",
            "--skip-git-repo-check",
            "-s",
            "read-only",
            "-c",
            "approval_policy='never'",
            "-C",
            workingDirectory,
            "-c",
            $"mcp_servers.{McpServerName}.command={ToTomlString(_options.BridgePath)}",
            "-c",
            $"mcp_servers.{McpServerName}.args=[{ToTomlString("--pipe")},{ToTomlString(_options.PipeName)}]"
        };

        if (!string.IsNullOrWhiteSpace(_options.ModelId))
        {
            arguments.Add("-m");
            arguments.Add(_options.ModelId);
        }

        arguments.Add("-");

        Process process;
        try
        {
            process = new Process
            {
                StartInfo = CliProcessStartInfo.Create(_executablePath!, arguments, workingDirectory),
                EnableRaisingEvents = true
            };
            process.Start();
        }
        catch (Exception ex)
        {
            Emit(CompanionUiEvent.Diagnostic("Failed to start codex: " + ex.Message));
            Emit(CompanionUiEvent.SessionStatus("Codex error"));
            Emit(CompanionUiEvent.Input(true));
            return;
        }

        lock (_processLock)
        {
            _currentTurnProcess = process;
        }

        try
        {
            await process.StandardInput.WriteAsync(prompt).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            Task stdoutTask = ReadStdoutAsync(process.StandardOutput, cancellationToken);
            Task stderrTask = ReadStderrAsync(process.StandardError, cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            try
            {
                if (process.WaitForExit(5000) && process.ExitCode != 0)
                {
                    Emit(CompanionUiEvent.Diagnostic($"Codex exited with code {process.ExitCode}."));
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
        catch (Exception ex)
        {
            Emit(CompanionUiEvent.Diagnostic("Codex stream error: " + ex.Message));
        }
        finally
        {
            lock (_processLock)
            {
                if (ReferenceEquals(_currentTurnProcess, process))
                {
                    _currentTurnProcess = null;
                }
            }

            try { process.Dispose(); } catch (InvalidOperationException) { }

            // Commit the turn to in-memory history so the next exec sees it.
            _history.Add(("user", text));
            string assistantReply = _currentTurnAssistant.ToString().Trim();
            if (assistantReply.Length > 0)
            {
                _history.Add(("assistant", assistantReply));
            }
            TrimHistoryToBudget();

            Emit(CompanionUiEvent.SessionStatus("Codex ready"));
            Emit(CompanionUiEvent.Input(true));
        }
    }

    public void Stop()
    {
        Process? process;
        lock (_processLock)
        {
            process = _currentTurnProcess;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private async Task ReadStdoutAsync(TextReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            DispatchJsonlLine(line);
        }
    }

    private async Task ReadStderrAsync(TextReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (ShouldSuppressCodexOutputLine(line))
            {
                continue;
            }

            Emit(CompanionUiEvent.Diagnostic("codex: " + line));
        }
    }

    private void DispatchJsonlLine(string line)
    {
        JsonDocument? document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            if (ShouldSuppressCodexOutputLine(line))
            {
                return;
            }

            Emit(CompanionUiEvent.Diagnostic("codex (non-JSON): " + line));
            return;
        }

        try
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string type = ReadString(root, "type") ?? "unknown";

            switch (type)
            {
                case "thread.started":
                    _lastThreadId = ReadString(root, "thread_id");
                    break;

                case "turn.started":
                    // already announced via SessionStatus("Codex working") on send
                    break;

                case "turn.completed":
                    Emit(CompanionUiEvent.Result(null));
                    break;

                case "turn.failed":
                    Emit(CompanionUiEvent.Diagnostic(
                        "Codex turn failed: " + (ReadNestedString(root, "error", "message") ?? root.GetRawText())));
                    break;

                case "error":
                    Emit(CompanionUiEvent.Diagnostic(
                        "Codex error: " + (ReadString(root, "message") ?? root.GetRawText())));
                    break;

                default:
                    if (type.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
                    {
                        DispatchItemEvent(type, root);
                    }
                    else
                    {
                        DiagnoseOnce(type, () => "codex event: " + type);
                    }
                    break;
            }
        }
        finally
        {
            document.Dispose();
        }
    }

    private void DispatchItemEvent(string type, JsonElement root)
    {
        // codex item events generally carry an "item" object with the payload, or
        // place the payload directly on the root. Probe both.
        JsonElement payload = TryGetObjectProperty(root, "item", out JsonElement itemProperty)
            ? itemProperty
            : root;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string? itemType = ReadString(payload, "type") ?? ReadString(payload, "kind");

        // 1) Plain assistant message text
        string? text = ReadAnyText(payload);
        if (!string.IsNullOrWhiteSpace(text))
        {
            // Treat reasoning/thinking-shaped items as thinking, otherwise assistant.
            string effectiveItemType = itemType ?? type;
            if (effectiveItemType.Contains("reason", StringComparison.OrdinalIgnoreCase)
                || effectiveItemType.Contains("think", StringComparison.OrdinalIgnoreCase))
            {
                Emit(CompanionUiEvent.Thinking(text));
            }
            else
            {
                Emit(CompanionUiEvent.Message("assistant", text));
                if (_currentTurnAssistant.Length > 0) _currentTurnAssistant.AppendLine();
                _currentTurnAssistant.Append(text);
            }
            return;
        }

        // 2) Tool / command call
        JsonElement toolElement = default;
        bool hasTool =
            TryGetObjectProperty(payload, "tool_call", out toolElement)
            || TryGetObjectProperty(payload, "command", out toolElement)
            || TryGetObjectProperty(payload, "mcp_tool_call", out toolElement);

        if (hasTool)
        {
            EmitToolEvent(type, payload, toolElement, itemType);
            return;
        }

        if (IsToolShapedItem(payload, itemType))
        {
            EmitToolEvent(type, payload, payload, itemType);
            return;
        }

        // Codex emits lifecycle markers like item.started for agent messages before
        // there is renderable text. They are expected and should not pollute chat.
        if (type.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DiagnoseOnce(type, () => "codex item not rendered: " + type);
    }

    private void EmitToolEvent(string eventType, JsonElement payload, JsonElement toolElement, string? itemType)
    {
        string toolUseId = ReadString(toolElement, "id") ?? ReadString(payload, "id") ?? Guid.NewGuid().ToString("N");
        string? serverName = ReadString(toolElement, "server") ?? ReadString(toolElement, "server_name");
        string toolName = ReadString(toolElement, "name")
            ?? ReadString(toolElement, "tool")
            ?? ReadString(toolElement, "tool_name")
            ?? ReadString(toolElement, "command")
            ?? itemType
            ?? "tool";

        if (!string.IsNullOrWhiteSpace(serverName)
            && !toolName.Contains('.', StringComparison.Ordinal))
        {
            toolName = serverName + "." + toolName;
        }

        string? toolInput = ReadAnyAsString(toolElement, "arguments")
            ?? ReadAnyAsString(toolElement, "input")
            ?? ReadAnyAsString(toolElement, "args")
            ?? ReadAnyAsString(payload, "arguments")
            ?? ReadAnyAsString(payload, "input")
            ?? ReadAnyAsString(payload, "args");
        string? toolResult = ReadAnyAsString(payload, "output")
            ?? ReadAnyAsString(payload, "result")
            ?? ReadAnyAsString(payload, "content")
            ?? ReadAnyAsString(payload, "error")
            ?? ReadAnyAsString(toolElement, "output")
            ?? ReadAnyAsString(toolElement, "result")
            ?? ReadAnyAsString(toolElement, "content")
            ?? ReadAnyAsString(toolElement, "error");
        string status = StatusForToolEvent(eventType, payload);

        Emit(CompanionUiEvent.Tool(status, toolUseId, toolName, toolInput, toolResult));
    }

    private static bool IsToolShapedItem(JsonElement payload, string? itemType)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (itemType is not null
            && (itemType.Contains("tool", StringComparison.OrdinalIgnoreCase)
                || itemType.Contains("function_call", StringComparison.OrdinalIgnoreCase)
                || itemType.Contains("command", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return (TryGetObjectProperty(payload, "name", out _)
                || TryGetObjectProperty(payload, "tool", out _)
                || TryGetObjectProperty(payload, "tool_name", out _))
            && (TryGetObjectProperty(payload, "arguments", out _)
                || TryGetObjectProperty(payload, "input", out _)
                || TryGetObjectProperty(payload, "args", out _)
                || TryGetObjectProperty(payload, "result", out _)
                || TryGetObjectProperty(payload, "output", out _)
                || TryGetObjectProperty(payload, "content", out _));
    }

    private static string StatusForToolEvent(string eventType, JsonElement payload)
    {
        string? itemStatus = ReadString(payload, "status");
        if (eventType.EndsWith(".failed", StringComparison.OrdinalIgnoreCase)
            || eventType.EndsWith(".cancelled", StringComparison.OrdinalIgnoreCase)
            || eventType.EndsWith(".canceled", StringComparison.OrdinalIgnoreCase)
            || HasErrorFlag(payload)
            || string.Equals(itemStatus, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemStatus, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemStatus, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        if (eventType.EndsWith(".completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(itemStatus, "success", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        return "running";
    }

    private static bool HasErrorFlag(JsonElement element)
    {
        if (TryGetObjectProperty(element, "is_error", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (TryGetObjectProperty(element, "status", out JsonElement status)
            && status.ValueKind == JsonValueKind.String
            && string.Equals(status.GetString(), "failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string? ReadAnyText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? direct = ReadString(element, "text");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (TryGetObjectProperty(element, "message", out JsonElement message))
        {
            string? messageText = ReadString(message, "text");
            if (!string.IsNullOrWhiteSpace(messageText))
            {
                return messageText;
            }

            if (TryGetObjectProperty(message, "content", out JsonElement content)
                && content.ValueKind == JsonValueKind.Array)
            {
                var sb = new System.Text.StringBuilder();
                foreach (JsonElement block in content.EnumerateArray())
                {
                    string? blockText = ReadString(block, "text");
                    if (!string.IsNullOrWhiteSpace(blockText))
                    {
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(blockText);
                    }
                }
                if (sb.Length > 0) return sb.ToString();
            }
        }

        if (TryGetObjectProperty(element, "content", out JsonElement contentDirect)
            && contentDirect.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (JsonElement block in contentDirect.EnumerateArray())
            {
                string? blockText = ReadString(block, "text");
                if (!string.IsNullOrWhiteSpace(blockText))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(blockText);
                }
            }
            if (sb.Length > 0) return sb.ToString();
        }

        string? reasoning = ReadString(element, "reasoning") ?? ReadString(element, "thinking");
        return string.IsNullOrWhiteSpace(reasoning) ? null : reasoning;
    }

    private static string? ReadAnyAsString(JsonElement element, string propertyName)
    {
        if (!TryGetObjectProperty(element, propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => property.GetRawText(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.GetRawText(),
            _ => null
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return TryGetObjectProperty(element, propertyName, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? ReadNestedString(JsonElement element, string outer, string inner)
    {
        return TryGetObjectProperty(element, outer, out JsonElement outerElement)
            ? ReadString(outerElement, inner)
            : null;
    }

    private static bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        return element.TryGetProperty(propertyName, out property);
    }

    private void DiagnoseOnce(string key, Func<string> messageFactory)
    {
        if (_diagnosticOnceSeen.Add(key))
        {
            Emit(CompanionUiEvent.Diagnostic(messageFactory()));
        }
    }

    private static bool ShouldSuppressCodexOutputLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.StartsWith("SUCCESS: The process with PID ", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains(" has been terminated", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("ERROR: The process \"", StringComparison.OrdinalIgnoreCase)
            && trimmed.EndsWith("\" not found.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(trimmed, "Reading additional input from stdin...", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.Contains(" WARN codex_core::plugins", StringComparison.Ordinal)
            || trimmed.Contains(" WARN codex_core_plugins::manifest", StringComparison.Ordinal)
            || trimmed.Contains(" WARN codex_core_skills::loader", StringComparison.Ordinal)
            || trimmed.Contains(" WARN codex_analytics::client", StringComparison.Ordinal)
            || trimmed.Contains("remote plugin sync", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("featured plugin ids", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("backend-api/plugins", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("backend-api/codex/analytics", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("__cf_chl", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("</", StringComparison.Ordinal)
            || trimmed.StartsWith("width=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("height=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("viewBox=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("fill=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("strokeWidth=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("class=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("d=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("xmlns=", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToTomlString(string value)
    {
        if (!value.Contains('\'') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return "'" + value + "'";
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private string BuildPromptWithHistory(string newUserText)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(BuildBoundDocumentPrompt());
        sb.AppendLine();
        sb.AppendLine();

        if (_history.Count > 0)
        {
            sb.AppendLine("[Conversation so far — for context only; treat the latest user message as the active request.]");
            foreach ((string role, string text) in _history)
            {
                sb.AppendLine();
                sb.Append('[').Append(role).Append(']').AppendLine();
                sb.AppendLine(text);
            }
            sb.AppendLine();
            sb.AppendLine("[Latest user message]");
        }

        sb.Append(newUserText);
        return sb.ToString();
    }

    private void TrimHistoryToBudget()
    {
        // Drop the oldest pairs until the in-memory transcript fits the budget.
        // Keep the most recent turns since recent context matters most for
        // continuation.
        int total = _history.Sum(entry => entry.Text.Length);
        while (total > HistoryCharBudget && _history.Count > 0)
        {
            (string _, string Text) = _history[0];
            _history.RemoveAt(0);
            total -= Text.Length;
        }
    }

    private string BuildBoundDocumentPrompt()
    {
        return string.Join(
            Environment.NewLine,
            "[System context - MCP_Rhino Companion bound session]",
            "You are running in MCP_Rhino Companion via the Codex CLI.",
            "This session is bound to exactly one saved Rhino document.",
            $"Bound Rhino document path: {_options.DocumentPath}",
            $"Bound Rhino runtime serial number: {_options.RuntimeSerial}",
            $"Bound MCP pipe: {_options.PipeName}",
            "Use the 'rhino' MCP server's tools for this bound document.",
            "When a Rhino MCP tool has a filePath parameter, always pass the bound document path shown above.",
            "Do not ask the user for the .3dm path for this companion session.",
            "Do not use local shell commands or direct .3dm file reads as a fallback; if a Rhino MCP tool fails, report the MCP failure.",
            "The panel-bound MCP server resolves tool calls by the bound Rhino runtime serial number; it does not follow RhinoDoc.ActiveDoc.",
            "Do not try to operate on any other open Rhino document from this companion window.",
            "Before destructive mutations, inspect or preview as appropriate, then apply changes only to the bound document.");
    }

    private void Emit(CompanionUiEvent uiEvent)
    {
        EventReceived?.Invoke(this, uiEvent);
    }
}
