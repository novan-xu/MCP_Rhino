using System.Text.Json;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;
using MCP_Rhino.Server.Infrastructure.Plugin;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string MultiRhinoPanelPipesSlug = "multi-rhino-panel-pipes-smoke-test";

    partial void RegisterMultiRhinoPanelPipeHandlers()
    {
        _extensionHandlers[MultiRhinoPanelPipesSlug] = HandleMultiRhinoPanelPipesSmokeTest;
    }

    private static bool HandleMultiRhinoPanelPipesSmokeTest(string[] args)
    {
        try
        {
            const uint runtimeSerial = 12345;
            string panelPipeName = McpPipeNames.ForPanelBoundDocument(runtimeSerial);
            string expectedPanelPipeName = $"mcp_rhino_{Environment.ProcessId}_{runtimeSerial}";

            RequireMultiRhino(
                McpPipeNames.DeveloperDebugPipeName == "mcp_rhino",
                "Developer debug pipe name changed unexpectedly.");
            RequireMultiRhino(
                panelPipeName == expectedPanelPipeName,
                "Panel-bound pipe name must include process id and runtime serial.");
            RequireMultiRhino(
                panelPipeName != $"mcp_rhino_{runtimeSerial}",
                "Panel-bound pipe name must not use runtime serial only.");

            using JsonDocument config = JsonDocument.Parse(McpConfigBuilder.BuildJson("C:\\MCP_Rhino.Bridge.exe", panelPipeName));
            JsonElement rhinoServer = config.RootElement.GetProperty("mcpServers").GetProperty("rhino");
            string configuredPipe = rhinoServer.GetProperty("args")[1].GetString() ?? string.Empty;
            RequireMultiRhino(
                configuredPipe == panelPipeName,
                "Generated MCP config must target the process-scoped panel pipe.");

            string tempA = McpConfigBuilder.GetTempDirectory(1, "mcp_rhino_111_1");
            string tempB = McpConfigBuilder.GetTempDirectory(1, "mcp_rhino_222_1");
            RequireMultiRhino(
                !string.Equals(tempA, tempB, StringComparison.OrdinalIgnoreCase),
                "Temporary MCP config directories must be pipe-name scoped.");

            string root = FindMultiRhinoRepositoryRoot();
            RequireSourceContains(
                root,
                Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "MCP_Rhino.RhinoPlugin.cs"),
                "McpPipeNames.ForPanelBoundDocument(serial)");
            RequireSourceContains(
                root,
                Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "Panel", "RhinoChatPanelHost.cs"),
                "McpPipeNames.ForPanelBoundDocument(serial)");
            RequireSourceContains(
                root,
                Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "McpNamedPipeServer.cs"),
                "stopOnPipeCreateFailure");
            RequireSourceContains(
                root,
                Path.Combine("src", "MCP_Rhino.Bridge", "Program.cs"),
                "mcp_rhino_<processId>_<runtimeSerial>");
            RequireSourceContains(
                root,
                Path.Combine("Project_Guides", "MCP_Rhino Architecture.md"),
                "mcp_rhino_<ProcessId>_<RuntimeSerialNumber>");

            Console.WriteLine($"[OK] Developer debug pipe remains {McpPipeNames.DeveloperDebugPipeName}.");
            Console.WriteLine($"[OK] Panel-bound pipe is process-scoped: {panelPipeName}.");
            Console.WriteLine("[OK] Temporary MCP config directories are pipe-name scoped.");
            Console.WriteLine("[OK] Project guide documents multi-Rhino pipe isolation.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Multi-Rhino panel pipes smoke failed: {ex}");
            Environment.ExitCode = 1;
            return true;
        }
    }

    private static void RequireSourceContains(string root, string relativePath, string expected)
    {
        string path = Path.Combine(
            new[] { root }.Concat(relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToArray());
        string source = File.ReadAllText(path);
        RequireMultiRhino(
            source.Contains(expected, StringComparison.Ordinal),
            $"{relativePath} should contain [{expected}].");
    }

    private static string FindMultiRhinoRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "MCP_Rhino.sln"))
                && Directory.Exists(Path.Combine(directory, "Project_Guides")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void RequireMultiRhino(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
