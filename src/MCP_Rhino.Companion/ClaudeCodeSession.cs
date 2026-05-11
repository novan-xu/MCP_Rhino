using System.Diagnostics;
using System.IO;

namespace MCP_Rhino.Companion;

public sealed class ClaudeCodeSession : IAgentSession
{
    private const string DisallowedTools = "Bash,Edit,Read,Write,Grep,Glob,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit,Monitor,TaskOutput,TaskStop,CronCreate,CronDelete,CronList,EnterPlanMode,ExitPlanMode,EnterWorktree,ExitWorktree,PushNotification,RemoteTrigger,AskUserQuestion";

    private readonly CompanionOptions _options;
    private CancellationTokenSource _shutdown = new();
    private Process? _process;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private bool _inputEnabled;
    // Captured from claude's stream events (`session_id`) on the first turn so
    // subsequent restarts can `--resume` the same conversation rather than
    // start a fresh one with no memory.
    private string? _capturedSessionId;

    public ClaudeCodeSession(CompanionOptions options)
    {
        _options = options;
    }

    public event EventHandler<CompanionUiEvent>? EventReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                return;
            }

            _process.Dispose();
            _process = null;
        }

        if (_shutdown.IsCancellationRequested)
        {
            _shutdown.Dispose();
            _shutdown = new CancellationTokenSource();
        }

        Emit(CompanionUiEvent.SessionStatus("Starting"));
        Emit(CompanionUiEvent.Input(false));

        if (!File.Exists(_options.BridgePath))
        {
            Emit(CompanionUiEvent.Diagnostic("Bridge not found: " + _options.BridgePath));
            Emit(CompanionUiEvent.SessionStatus("Bridge missing"));
            return;
        }

        ClaudeCodeAvailabilityResult availability = ClaudeCodeAvailability.Check();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
        {
            Emit(CompanionUiEvent.Diagnostic(availability.Message));
            Emit(CompanionUiEvent.SessionStatus("Claude Code missing"));
            return;
        }

        string configPath = McpConfigBuilder.WriteConfig(_options.RuntimeSerial, _options.BridgePath, _options.PipeName);
        string workingDirectory = CompanionWorkspace.GetWorkingDirectory(_options.PipeName);
        var arguments = new List<string>
        {
            "--print",
            "--output-format=stream-json",
            "--input-format=stream-json",
            "--verbose",
            "--no-session-persistence",
            "--setting-sources",
            "user",
            "--tools",
            string.Empty,
            "--mcp-config",
            configPath,
            "--strict-mcp-config",
            "--permission-mode",
            "bypassPermissions",
            "--disallowedTools",
            DisallowedTools,
            "--append-system-prompt",
            BuildBoundDocumentPrompt()
        };

        // If we previously ran in this Companion launch and captured a session
        // id, resume that conversation so the agent retains its memory across
        // process exits.
        if (!string.IsNullOrWhiteSpace(_capturedSessionId))
        {
            arguments.Add("--resume");
            arguments.Add(_capturedSessionId);
        }

        if (!string.IsNullOrWhiteSpace(_options.ModelId))
        {
            arguments.Add("--model");
            arguments.Add(_options.ModelId);
        }

        _process = new Process
        {
            StartInfo = ClaudeCodeAvailability.CreateStartInfo(availability.ExecutablePath, arguments, workingDirectory),
            EnableRaisingEvents = true
        };
        Process process = _process;
        process.Exited += (_, _) =>
        {
            _inputEnabled = false;
            Emit(CompanionUiEvent.Input(false));
            Emit(CompanionUiEvent.SessionStatus("Claude Code exited"));
            try
            {
                Emit(CompanionUiEvent.Diagnostic($"Claude Code exited with code {process.ExitCode}."));
            }
            catch (InvalidOperationException)
            {
            }
        };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            _process = null;
            Emit(CompanionUiEvent.Diagnostic("Failed to start Claude Code: " + ex.Message));
            Emit(CompanionUiEvent.SessionStatus("Claude Code error"));
            return;
        }

        _stdoutTask = StreamJsonReader.ReadEventsAsync(
            process.StandardOutput,
            OnStreamEvent,
            line => Emit(CompanionUiEvent.Diagnostic("Unparsed stdout: " + line)),
            _shutdown.Token);
        _stderrTask = ReadStderrAsync(process.StandardError, _shutdown.Token);

        _inputEnabled = true;
        Emit(CompanionUiEvent.Input(true));
        Emit(CompanionUiEvent.SessionStatus("Claude Code ready"));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task SendUserMessageAsync(CompanionUserMessage message, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
        {
            Emit(CompanionUiEvent.Diagnostic("Claude Code was not running; restarting."));
            return RestartAndSendAsync(message, cancellationToken);
        }

        if (!_inputEnabled || _process is null || _process.HasExited)
        {
            Emit(CompanionUiEvent.Diagnostic("Input is disabled because Claude Code is not running."));
            return Task.CompletedTask;
        }

        Emit(CompanionUiEvent.Message("user", message.Text, message.AttachmentSummaries));
        return StreamJsonWriter.WriteUserMessageAsync(_process.StandardInput, message, cancellationToken);
    }

    private async Task RestartAndSendAsync(CompanionUserMessage message, CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        if (!_inputEnabled || _process is null || _process.HasExited)
        {
            Emit(CompanionUiEvent.Diagnostic("Input is disabled because Claude Code is not running."));
            return;
        }

        Emit(CompanionUiEvent.Message("user", message.Text, message.AttachmentSummaries));
        await StreamJsonWriter.WriteUserMessageAsync(_process.StandardInput, message, cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        _inputEnabled = false;
        Emit(CompanionUiEvent.Input(false));

        if (_process is null)
        {
            return;
        }

        _shutdown.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.StandardInput.Close();
                if (!_process.WaitForExit(500))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            McpConfigBuilder.DeleteTempDirectory(_options.RuntimeSerial, _options.PipeName);
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();

        try
        {
            Task.WaitAll(new[] { _stdoutTask, _stderrTask }.Where(task => task is not null).Cast<Task>().ToArray(), 1000);
        }
        catch (AggregateException)
        {
        }

        _process?.Dispose();
        _shutdown.Dispose();
    }

    private void OnStreamEvent(ClaudeCodeStreamEvent streamEvent)
    {
        // Capture session_id from the first event that carries it so we can
        // --resume the same conversation on any subsequent restart.
        if (string.IsNullOrWhiteSpace(_capturedSessionId)
            && !string.IsNullOrWhiteSpace(streamEvent.SessionId))
        {
            _capturedSessionId = streamEvent.SessionId;
        }

        if (streamEvent.McpServers is { Count: > 0 })
        {
            string status = string.Join(", ", streamEvent.McpServers.Select(server => $"{server.Name}:{server.Status}"));
            Emit(CompanionUiEvent.SessionStatus("MCP " + status));
            if (streamEvent.McpServers.Any(server => string.Equals(server.Status, "failed", StringComparison.OrdinalIgnoreCase)))
            {
                _inputEnabled = false;
                Emit(CompanionUiEvent.Input(false));
            }
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.Thinking))
        {
            Emit(CompanionUiEvent.Thinking(streamEvent.Thinking));
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.ToolName))
        {
            Emit(CompanionUiEvent.Tool(
                "running",
                streamEvent.ToolUseId,
                streamEvent.ToolName,
                streamEvent.ToolInputSummary,
                null));
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.ToolResultSummary))
        {
            Emit(CompanionUiEvent.Tool(
                streamEvent.ToolResultIsError ? "failed" : "success",
                streamEvent.ToolUseId,
                streamEvent.ToolName,
                null,
                streamEvent.ToolResultSummary));
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.Text))
        {
            Emit(CompanionUiEvent.Message("assistant", streamEvent.Text));
        }

        // The parser stores Type as "<type>/<subtype>" when a subtype is
        // present (e.g. "result/success", "result/error_max_turns"), so an
        // exact equality check would miss the turn-end signal and the UI
        // would stay busy. Match the bare "result" prefix instead.
        if (streamEvent.Type.StartsWith("result", StringComparison.OrdinalIgnoreCase))
        {
            Emit(CompanionUiEvent.Result(streamEvent.TotalCostUsd));
            Emit(CompanionUiEvent.Input(true));
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

            Emit(CompanionUiEvent.Diagnostic(line));
        }
    }

    private string BuildBoundDocumentPrompt()
    {
        string boundDocumentPrompt = string.Join(
            Environment.NewLine,
            "You are running in MCP_Rhino Companion.",
            "This Claude Code session is bound to exactly one saved Rhino document.",
            $"Bound Rhino document path: {_options.DocumentPath}",
            $"Bound Rhino runtime serial number: {_options.RuntimeSerial}",
            $"Bound MCP pipe: {_options.PipeName}",
            "Use the Rhino MCP tools directly for this bound document.",
            "When a Rhino MCP tool has a filePath parameter, always pass the bound Rhino document path shown above.",
            "Do not ask the user for the .3dm path for this companion session.",
            "Do not use local shell commands or direct .3dm file reads as a fallback; if a Rhino MCP tool fails, report the MCP failure.",
            "Do not open, read, write, monitor, index, or otherwise touch the bound .3dm file through local file tools.",
            "The panel-bound MCP server resolves tool calls by the bound Rhino runtime serial number; it does not follow RhinoDoc.ActiveDoc.",
            "The filePath argument exists for tool schema compatibility and is ignored for routing by the panel-bound server.",
            "Do not try to operate on any other open Rhino document from this companion window.",
            "If the user says this file, current file, active model, the model, or similar, they mean the bound Rhino document.",
            "Before destructive mutations, inspect or preview as appropriate, then apply changes only to the bound document.");

        return boundDocumentPrompt
            + Environment.NewLine
            + Environment.NewLine
            + RuntimePolicyPrompt.Load();
    }

    private void Emit(CompanionUiEvent uiEvent)
    {
        EventReceived?.Invoke(this, uiEvent);
    }
}
