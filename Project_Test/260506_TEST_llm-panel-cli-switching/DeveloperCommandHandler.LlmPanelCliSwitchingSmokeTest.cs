using System.Diagnostics;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterLlmPanelCliSwitchingHandlers()
    {
        _extensionHandlers[LlmPanelCliSwitchingSmokeTest.Slug] = HandleLlmPanelCliSwitchingSmokeTest;
    }

    private bool HandleLlmPanelCliSwitchingSmokeTest(string[] args)
    {
        try
        {
            RunCommandScriptLaunchSmoke();
            RunCommandScriptUtf8StdinSmoke();
            RunCompanionProviderModelContractSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LLM panel CLI switching smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunCommandScriptLaunchSmoke()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "mcp-rhino-cli-switching-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string scriptPath = Path.Combine(tempDirectory, "fake codex.cmd");

        try
        {
            File.WriteAllText(
                scriptPath,
                string.Join(
                    Environment.NewLine,
                    "@echo off",
                    "echo script=%~f0",
                    "echo args=%*"));

            using var process = new Process();
            process.StartInfo = ClaudeCodeAvailability.CreateStartInfo(
                scriptPath,
                new[]
                {
                    "--version",
                    "-c",
                    "mcp_servers.rhino.command='C:\\MCP Rhino\\MCP_Rhino.Bridge.exe'",
                    "-C",
                    "C:\\Workspace With Spaces"
                },
                tempDirectory);
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Command script launch smoke timed out.");
            }

            RequireLlmPanelCli(process.ExitCode == 0, "Command script exits successfully through cmd.exe call.");
            RequireLlmPanelCli(output.Contains("fake codex.cmd", StringComparison.OrdinalIgnoreCase), "Command script path with spaces is executable.");
            RequireLlmPanelCli(output.Contains("--version", StringComparison.Ordinal), "Command script receives normal arguments.");
            RequireLlmPanelCli(output.Contains("mcp_servers.rhino.command", StringComparison.Ordinal), "Command script receives Codex config arguments.");
            RequireLlmPanelCli(!error.Contains("not recognized", StringComparison.OrdinalIgnoreCase), "Command script path is not treated as a literal quoted command.");
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch { }
        }
    }

    private static void RunCommandScriptUtf8StdinSmoke()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "mcp-rhino-cli-utf8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string scriptPath = Path.Combine(tempDirectory, "utf8 stdin codex.cmd");

        try
        {
            File.WriteAllText(
                scriptPath,
                string.Join(
                    Environment.NewLine,
                    "@echo off",
                    "powershell -NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; $ms=[System.IO.MemoryStream]::new(); [Console]::OpenStandardInput().CopyTo($ms); $bytes=$ms.ToArray(); $text=[System.Text.UTF8Encoding]::new($false,$true).GetString($bytes); if ($text.Contains([char]0x2014) -and $text.Contains([char]0x00E9)) { exit 0 }; [Console]::Error.WriteLine('decoded text missing expected unicode chars'); exit 3\""));

            using var process = new Process();
            process.StartInfo = ClaudeCodeAvailability.CreateStartInfo(scriptPath, Array.Empty<string>(), tempDirectory);
            process.Start();

            process.StandardInput.Write("System context \u2014 caf\u00e9");
            process.StandardInput.Close();

            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("UTF-8 stdin smoke timed out.");
            }

            RequireLlmPanelCli(process.ExitCode == 0, "Command script receives redirected prompt stdin as valid UTF-8.");
            RequireLlmPanelCli(!error.Contains("invalid", StringComparison.OrdinalIgnoreCase), "UTF-8 stdin validation does not report invalid bytes.");
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch { }
        }
    }

    private static void RunCompanionProviderModelContractSmoke()
    {
        string root = Directory.GetCurrentDirectory();
        string appJsPath = Path.Combine(root, "src", "MCP_Rhino.Companion", "wwwroot", "app.js");
        string eventPath = Path.Combine(root, "src", "MCP_Rhino.Companion", "CompanionUiEvent.cs");
        string mainWindowPath = Path.Combine(root, "src", "MCP_Rhino.Companion", "MainWindow.xaml.cs");
        string codexSessionPath = Path.Combine(root, "src", "MCP_Rhino.Companion", "CodexCliSession.cs");
        string companionLauncherPath = Path.Combine(root, "src", "MCP_Rhino.Companion", "CliProcessStartInfo.cs");
        string userAttributeFilterPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Tools", "Analysis", "FilterObjectsByUserAttributesTool.cs");

        string appJs = File.ReadAllText(appJsPath);
        string uiEvent = File.ReadAllText(eventPath);
        string mainWindow = File.ReadAllText(mainWindowPath);
        string codexSession = File.ReadAllText(codexSessionPath);
        string companionLauncher = File.ReadAllText(companionLauncherPath);
        string userAttributeFilter = File.ReadAllText(userAttributeFilterPath);

        RequireLlmPanelCli(appJs.Contains("FALLBACK_MODELS_BY_CLI", StringComparison.Ordinal), "Frontend keeps provider-specific fallback model lists.");
        RequireLlmPanelCli(appJs.Contains("\"codex cli\"", StringComparison.Ordinal), "Frontend includes Codex as a selectable CLI.");
        RequireLlmPanelCli(appJs.Contains("\"gpt-5.5\"", StringComparison.Ordinal), "Frontend Codex model list includes OpenAI model IDs.");
        RequireLlmPanelCli(appJs.Contains("event.models", StringComparison.Ordinal), "Frontend accepts host-owned model lists.");
        RequireLlmPanelCli(appJs.Contains("post(\"model\"", StringComparison.Ordinal), "Frontend sends model changes to the host.");
        RequireLlmPanelCli(appJs.Contains("let mcpConnected = false", StringComparison.Ordinal), "Frontend preserves MCP connected state across provider status updates.");
        RequireLlmPanelCli(appJs.Contains("lower.includes(\"ready\")", StringComparison.Ordinal), "Frontend treats provider-ready status as MCP available for Codex.");

        RequireLlmPanelCli(uiEvent.Contains("string? Cli", StringComparison.Ordinal), "Session events carry the active CLI.");
        RequireLlmPanelCli(uiEvent.Contains("IReadOnlyList<string>? Models", StringComparison.Ordinal), "Session events carry provider model choices.");
        RequireLlmPanelCli(mainWindow.Contains("case \"model\"", StringComparison.Ordinal), "Host handles model selection messages.");
        RequireLlmPanelCli(mainWindow.Contains("AgentCliCatalog.ModelsFor", StringComparison.Ordinal), "Host sends provider-specific model choices.");
        RequireLlmPanelCli(codexSession.Contains("command={ToTomlString", StringComparison.Ordinal), "Codex MCP command config is emitted as a TOML value.");
        RequireLlmPanelCli(!codexSession.Contains("command=\\\"", StringComparison.Ordinal), "Codex MCP command config avoids embedded double quotes in the command-line argument.");
        RequireLlmPanelCli(codexSession.Contains("approval_policy='never'", StringComparison.Ordinal), "Codex exec runs with non-interactive approval policy through config.");
        RequireLlmPanelCli(!codexSession.Contains("\"-a\"", StringComparison.Ordinal), "Codex exec does not emit unsupported approval shorthand.");
        RequireLlmPanelCli(codexSession.Contains("IsToolShapedItem", StringComparison.Ordinal), "Codex item payloads can be rendered when the item itself is the tool call.");
        RequireLlmPanelCli(codexSession.Contains("mcp_tool_call", StringComparison.Ordinal), "Codex MCP tool-call item type is recognized.");
        RequireLlmPanelCli(codexSession.Contains("ShouldSuppressCodexOutputLine", StringComparison.Ordinal), "Codex non-JSON cleanup noise is filtered.");
        RequireLlmPanelCli(codexSession.Contains("not found.", StringComparison.Ordinal), "Codex missing-process cleanup noise is filtered.");
        RequireLlmPanelCli(codexSession.Contains("TryGetObjectProperty", StringComparison.Ordinal), "Codex parser guards object-only JSON access.");
        RequireLlmPanelCli(codexSession.Contains("Do not use local shell commands", StringComparison.Ordinal), "Codex prompt blocks shell fallback after MCP failures.");
        RequireLlmPanelCli(companionLauncher.Contains("StandardInputEncoding", StringComparison.Ordinal), "Companion CLI launcher pins redirected stdin encoding.");
        RequireLlmPanelCli(companionLauncher.Contains("UTF8Encoding", StringComparison.Ordinal), "Companion CLI launcher uses UTF-8 for redirected streams.");
        RequireLlmPanelCli(userAttributeFilter.Contains("ReadOnly = true", StringComparison.Ordinal), "User-attribute filter advertises read-only MCP safety.");
        RequireLlmPanelCli(userAttributeFilter.Contains("Destructive = false", StringComparison.Ordinal), "User-attribute filter advertises non-destructive MCP safety.");
        RequireLlmPanelCli(userAttributeFilter.Contains("OpenWorld = false", StringComparison.Ordinal), "User-attribute filter advertises closed-world MCP safety.");
    }

    private static void RequireLlmPanelCli(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine($"[OK] {message}");
    }
}
