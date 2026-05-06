extern alias rhinocommon;

using System.Diagnostics;
using System.Text.Json;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Plugin.Companion;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterRhinoClaudeCodeCompanionUiHandlers()
    {
        _extensionHandlers[RhinoClaudeCodeCompanionUiSmokeTest.Slug] = HandleRhinoClaudeCodeCompanionUiSmokeTest;
    }

    private bool HandleRhinoClaudeCodeCompanionUiSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunRhinoClaudeCodeCompanionUiCliSmoke();
            }
            else
            {
                RunRhinoClaudeCodeCompanionUiLiveSmoke();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rhino Claude Code Companion UI smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunRhinoClaudeCodeCompanionUiCliSmoke()
    {
        OperationResponse<string> liveResult = _liveRhinoDocumentAccessor.Execute(
            "not-a-real-file.3dm",
            _ => OperationResponse<string>.Ok("unexpected"));
        RequireCompanionUi(!liveResult.Success && liveResult.Message == "LIVE_RHINO_REQUIRED",
            "CLI mode keeps the Rhino live accessor disabled.");

        string configJson = McpConfigBuilder.BuildJson(
            "C:/MCP_Rhino/MCP_Rhino.Bridge.exe",
            "mcp_rhino_12345");
        using (JsonDocument.Parse(configJson))
        {
        }

        RequireCompanionUi(configJson.Contains("mcp_rhino_12345", StringComparison.Ordinal),
            "Per-document MCP config JSON is parseable and contains the bound pipe name.");

        ClaudeCodeAvailabilityResult availability = ClaudeCodeAvailability.Check();
        Console.WriteLine($"[OK] Claude Code availability probe completed: {availability.Message}");

        string? companionPath = FindBuiltCompanionExecutable();
        if (companionPath is null)
        {
            Console.WriteLine("[OK] Companion --validate-args check skipped because MCP_Rhino.Companion.exe is not built yet.");
            return;
        }

        string fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "Runtime_Test", "MCP_rhino_test.3dm");
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = companionPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("--validate-args");
        process.StartInfo.ArgumentList.Add("--document-path");
        process.StartInfo.ArgumentList.Add(fixturePath);
        process.StartInfo.ArgumentList.Add("--runtime-serial");
        process.StartInfo.ArgumentList.Add("12345");
        process.StartInfo.ArgumentList.Add("--pipe");
        process.StartInfo.ArgumentList.Add("mcp_rhino_12345");
        process.StartInfo.ArgumentList.Add("--bridge");
        process.StartInfo.ArgumentList.Add("C:\\MCP_Rhino\\MCP_Rhino.Bridge.exe");
        process.Start();

        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("MCP_Rhino.Companion.exe --validate-args did not exit within 5 seconds.");
        }

        RequireCompanionUi(process.ExitCode == 0,
            "MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.");
    }

    private static void RunRhinoClaudeCodeCompanionUiLiveSmoke()
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null || string.IsNullOrWhiteSpace(document.Path))
        {
            throw new InvalidOperationException("Rhino Claude Code Companion UI live smoke requires a saved active document.");
        }

        var accessor = new BoundLiveRhinoDocumentAccessor(document.RuntimeSerialNumber);
        bool stateMatched = accessor.TryGetActiveDocumentState("intentionally-wrong-path.3dm", out _);
        RequireCompanionUi(stateMatched, "Bound accessor ignores request FilePath when checking document state.");

        OperationResponse<string> result = accessor.Execute(
            "intentionally-wrong-path.3dm",
            boundDoc => OperationResponse<string>.Ok(boundDoc.Path));
        RequireCompanionUi(result.Success && string.Equals(result.Data, document.Path, StringComparison.OrdinalIgnoreCase),
            "Bound accessor resolves mutations and reads to the panel-bound Rhino document.");

        string pluginDirectory = Path.GetDirectoryName(typeof(McpRhinoPlugin).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to determine plugin assembly directory.");
        string? companionPath = CompanionProcessLauncher.FindCompanionExecutable(pluginDirectory);
        RequireCompanionUi(!string.IsNullOrWhiteSpace(companionPath),
            "Companion executable can be resolved from the Rhino plugin directory.");

        string pipeName = "mcp_rhino_companion_smoke_" + Guid.NewGuid().ToString("N");
        McpRhinoPlugin.Instance!.StartBoundPipeServer(pipeName, document.RuntimeSerialNumber);
        Console.WriteLine($"[OK] Started companion UI bound smoke pipe: {pipeName}");
        McpRhinoPlugin.Instance.StopBoundPipeServer(pipeName);
        Console.WriteLine($"[OK] Stopped companion UI bound smoke pipe: {pipeName}");
    }

    private static void RequireCompanionUi(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine($"[OK] {message}");
    }

    private static string? FindBuiltCompanionExecutable()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        foreach (string candidate in new[]
        {
            Path.Combine(currentDirectory, "src", "MCP_Rhino.Companion", "bin", "Release", "net8.0-windows", "MCP_Rhino.Companion.exe"),
            Path.Combine(currentDirectory, "src", "MCP_Rhino.Companion", "bin", "Debug", "net8.0-windows", "MCP_Rhino.Companion.exe")
        })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
