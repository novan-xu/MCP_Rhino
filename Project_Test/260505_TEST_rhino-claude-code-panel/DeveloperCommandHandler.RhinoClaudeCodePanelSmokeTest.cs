extern alias rhinocommon;

using System.Diagnostics;
using System.Text.Json;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Server.Infrastructure.Rhino.Live;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    partial void RegisterRhinoClaudeCodePanelHandlers()
    {
        _extensionHandlers[RhinoClaudeCodePanelSmokeTest.Slug] = HandleRhinoClaudeCodePanelSmokeTest;
    }

    private bool HandleRhinoClaudeCodePanelSmokeTest(string[] args)
    {
        try
        {
            if (McpRhinoPlugin.Instance is null)
            {
                RunRhinoClaudeCodePanelCliSmoke();
            }
            else
            {
                RunRhinoClaudeCodePanelLiveSmoke();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rhino Claude Code panel smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private void RunRhinoClaudeCodePanelCliSmoke()
    {
        OperationResponse<string> liveResult = _liveRhinoDocumentAccessor.Execute(
            "not-a-real-file.3dm",
            _ => OperationResponse<string>.Ok("unexpected"));
        Require(!liveResult.Success && liveResult.Message == "LIVE_RHINO_REQUIRED",
            "CLI mode does not instantiate a live or bound Rhino accessor.");

        string configJson = McpConfigBuilder.BuildJson(
            "C:/MCP_Rhino/MCP_Rhino.Bridge.exe",
            "mcp_rhino_12345");
        using (JsonDocument.Parse(configJson))
        {
        }
        Require(configJson.Contains("mcp_rhino_12345", StringComparison.Ordinal),
            "McpConfigBuilder generated parseable per-doc MCP config JSON.");

        ClaudeCodeAvailabilityResult availability = ClaudeCodeAvailability.Check();
        Console.WriteLine($"[OK] ClaudeCodeAvailability probe completed: {availability.Message}");

        string? bridgePath = FindBuiltBridgeExecutable();
        if (bridgePath is null)
        {
            Console.WriteLine("[OK] Bridge --pipe help check skipped because MCP_Rhino.Bridge.exe is not built yet.");
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = bridgePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("--pipe");
        process.StartInfo.ArgumentList.Add("other_name");
        process.StartInfo.ArgumentList.Add("--help");
        process.Start();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);

        Require(process.ExitCode == 0 && stderr.Contains("Usage:", StringComparison.OrdinalIgnoreCase),
            "MCP_Rhino.Bridge.exe --pipe other_name --help returns usage with exit code 0.");
    }

    private static void RunRhinoClaudeCodePanelLiveSmoke()
    {
        RhinoDoc? document = RhinoDoc.ActiveDoc;
        if (document is null || string.IsNullOrWhiteSpace(document.Path))
        {
            throw new InvalidOperationException("Rhino Claude Code panel live smoke requires a saved active document.");
        }

        var accessor = new BoundLiveRhinoDocumentAccessor(document.RuntimeSerialNumber);
        bool stateMatched = accessor.TryGetActiveDocumentState("intentionally-wrong-path.3dm", out _);
        Require(stateMatched, "Bound accessor ignores request FilePath in TryGetActiveDocumentState.");

        OperationResponse<string> result = accessor.Execute(
            "intentionally-wrong-path.3dm",
            boundDoc => OperationResponse<string>.Ok(boundDoc.Path));
        Require(result.Success && string.Equals(result.Data, document.Path, StringComparison.OrdinalIgnoreCase),
            "Bound accessor rewrites request FilePath to the bound document path.");

        string pipeName = "mcp_rhino_smoke_" + Guid.NewGuid().ToString("N");
        McpRhinoPlugin.Instance!.StartBoundPipeServer(pipeName, document.RuntimeSerialNumber);
        Console.WriteLine($"[OK] Started bound smoke pipe: {pipeName}");
        McpRhinoPlugin.Instance.StopBoundPipeServer(pipeName);
        Console.WriteLine($"[OK] Stopped bound smoke pipe: {pipeName}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine($"[OK] {message}");
    }

    private static string? FindBuiltBridgeExecutable()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        foreach (string candidate in new[]
        {
            Path.Combine(currentDirectory, "src", "MCP_Rhino.Bridge", "bin", "Release", "net8.0", "MCP_Rhino.Bridge.exe"),
            Path.Combine(currentDirectory, "src", "MCP_Rhino.Bridge", "bin", "Debug", "net8.0", "MCP_Rhino.Bridge.exe")
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
