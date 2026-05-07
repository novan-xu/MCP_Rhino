using MCP_Rhino.Server.Domain.Enums;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class PanelChatSessionService : IPanelChatSession
{
    private const string DisallowedTools = "Bash,Edit,Read,Write,Grep,Glob,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit,Monitor,TaskOutput,TaskStop,CronCreate,CronDelete,CronList,EnterPlanMode,ExitPlanMode,EnterWorktree,ExitWorktree,PushNotification,RemoteTrigger,AskUserQuestion";

    private readonly string? _bridgeExecutablePath;
    private readonly IClaudeCodeProcess _claudeCodeProcess;
    private bool _started;

    public PanelChatSessionService(
        uint runtimeSerialNumber,
        string documentPath,
        string pipeName,
        string? bridgeExecutablePath,
        IClaudeCodeProcess claudeCodeProcess)
    {
        RuntimeSerialNumber = runtimeSerialNumber;
        DocumentPath = documentPath;
        PipeName = pipeName;
        _bridgeExecutablePath = bridgeExecutablePath;
        _claudeCodeProcess = claudeCodeProcess;
        _claudeCodeProcess.EventReceived += OnClaudeCodeEvent;
        _claudeCodeProcess.ErrorReceived += (_, line) => Emit(ChatRole.System, $"Diagnostics: {line}");
        _claudeCodeProcess.Exited += (_, exitCode) =>
        {
            IsInputEnabled = false;
            Emit(ChatRole.System, $"Claude Code exited ({exitCode?.ToString() ?? "unknown"}).");
        };
    }

    public event EventHandler<PanelChatEvent>? EventReceived;

    public uint RuntimeSerialNumber { get; }

    public string DocumentPath { get; private set; }

    public string PipeName { get; }

    public bool IsInputEnabled { get; private set; }

    public async Task StartAsync(string? modelId, CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        _started = true;

        if (string.IsNullOrWhiteSpace(_bridgeExecutablePath))
        {
            IsInputEnabled = false;
            Emit(ChatRole.System, "Bridge not found, panel disabled.");
            return;
        }

        ClaudeCodeAvailabilityResult availability = ClaudeCodeAvailability.Check();
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
        {
            IsInputEnabled = false;
            Emit(ChatRole.System, "Install Claude Code: https://docs.claude.com/en/docs/claude-code/setup");
            Emit(ChatRole.System, availability.Message);
            return;
        }

        string configPath = McpConfigBuilder.WriteConfig(RuntimeSerialNumber, _bridgeExecutablePath, PipeName);
        string workingDirectory = PanelChatWorkspace.GetWorkingDirectory(PipeName);

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
            BuildPanelBoundSystemPrompt()
        };

        if (!string.IsNullOrWhiteSpace(modelId))
        {
            arguments.Add("--model");
            arguments.Add(modelId);
        }

        _claudeCodeProcess.Start(availability.ExecutablePath, arguments, workingDirectory);
        IsInputEnabled = true;
        Emit(ChatRole.System, $"Claude Code ready. Pipe: {PipeName}");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public Task SendUserMessageAsync(string text, CancellationToken cancellationToken)
    {
        if (!IsInputEnabled)
        {
            Emit(ChatRole.System, "Input is disabled.");
            return Task.CompletedTask;
        }

        Emit(ChatRole.User, text);
        return _claudeCodeProcess.SendUserMessageAsync(text, cancellationToken);
    }

    public void UpdateDocumentPath(string documentPath)
    {
        DocumentPath = documentPath;
        Emit(ChatRole.System, $"Document path updated: {documentPath}");
    }

    public void Stop()
    {
        IsInputEnabled = false;
        _claudeCodeProcess.Stop();
        McpConfigBuilder.DeleteTempDirectory(RuntimeSerialNumber, PipeName);
    }

    public void Dispose()
    {
        Stop();
        _claudeCodeProcess.Dispose();
    }

    private void OnClaudeCodeEvent(object? sender, ClaudeCodeStreamEvent streamEvent)
    {
        if (streamEvent.McpServers is { Count: > 0 })
        {
            string status = string.Join(", ", streamEvent.McpServers.Select(server => $"{server.Name}:{server.Status}"));
            Emit(ChatRole.System, $"MCP status: {status}");
            if (streamEvent.McpServers.Any(server => string.Equals(server.Status, "failed", StringComparison.OrdinalIgnoreCase)))
            {
                IsInputEnabled = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.ToolName))
        {
            Emit(ChatRole.Assistant, streamEvent.ToolInputSummary ?? string.Empty, streamEvent.ToolName, ChatToolCallStatus.Running);
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.ToolResultSummary))
        {
            Emit(ChatRole.Assistant, streamEvent.ToolResultSummary, streamEvent.ToolName, ChatToolCallStatus.Success);
        }

        if (!string.IsNullOrWhiteSpace(streamEvent.Text))
        {
            Emit(ChatRole.Assistant, streamEvent.Text);
        }

        if (string.Equals(streamEvent.Type, "result", StringComparison.OrdinalIgnoreCase))
        {
            string cost = streamEvent.TotalCostUsd.HasValue
                ? $" Cost: {streamEvent.TotalCostUsd.Value:0.####} USD."
                : string.Empty;
            Emit(ChatRole.System, $"Turn complete.{cost}");
        }
    }

    private void Emit(ChatRole role, string text, string? toolName = null, ChatToolCallStatus? toolStatus = null)
    {
        EventReceived?.Invoke(this, new PanelChatEvent(role, text, toolName, toolStatus));
    }

    private string BuildPanelBoundSystemPrompt()
    {
        return string.Join(
            Environment.NewLine,
            "You are running inside the MCP_Rhino Rhino panel.",
            "This Claude Code session is bound to exactly one saved Rhino document.",
            $"Bound Rhino document path: {DocumentPath}",
            $"Bound Rhino runtime serial number: {RuntimeSerialNumber}",
            $"Bound MCP pipe: {PipeName}",
            "Use the Rhino MCP tools directly for this bound document.",
            "When a Rhino MCP tool has a filePath parameter, always pass the bound Rhino document path shown above.",
            "Do not ask the user for the .3dm path for this panel session.",
            "Do not use local shell commands or direct .3dm file reads as a fallback; if a Rhino MCP tool fails, report the MCP failure.",
            "Do not open, read, write, monitor, index, or otherwise touch the bound .3dm file through local file tools.",
            "The panel-bound MCP server resolves tool calls by the bound Rhino runtime serial number; it does not follow RhinoDoc.ActiveDoc.",
            "The filePath argument exists for tool schema compatibility and is ignored for routing by the panel-bound server.",
            "Do not try to operate on any other open Rhino document from this panel.",
            "If the user says this file, current file, active model, the model, or similar, they mean the bound Rhino document.",
            "Before destructive mutations, inspect or preview as appropriate, then apply changes only to the bound document.");
    }
}
