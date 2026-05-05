using System.Text.Json;

namespace MCP_Rhino.Server.Infrastructure.ClaudeCode;

public static class McpConfigBuilder
{
    public static string BuildJson(string bridgeExecutablePath, string pipeName)
    {
        var config = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["rhino"] = new
                {
                    command = NormalizePath(bridgeExecutablePath),
                    args = new[] { "--pipe", pipeName }
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string WriteConfig(uint runtimeSerialNumber, string bridgeExecutablePath, string pipeName)
    {
        string directory = GetTempDirectory(runtimeSerialNumber);
        Directory.CreateDirectory(directory);

        string configPath = Path.Combine(directory, ".mcp.json");
        File.WriteAllText(configPath, BuildJson(bridgeExecutablePath, pipeName));
        return configPath;
    }

    public static string GetTempDirectory(uint runtimeSerialNumber)
    {
        return Path.Combine(Path.GetTempPath(), "MCP_Rhino", runtimeSerialNumber.ToString());
    }

    public static void DeleteTempDirectory(uint runtimeSerialNumber)
    {
        string directory = GetTempDirectory(runtimeSerialNumber);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static string? FindBridgeExecutable(string pluginDirectory)
    {
        foreach (string candidate in EnumerateBridgeCandidates(pluginDirectory))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateBridgeCandidates(string pluginDirectory)
    {
        yield return Path.Combine(pluginDirectory, "MCP_Rhino.Bridge.exe");
        yield return Path.GetFullPath(Path.Combine(
            pluginDirectory,
            "..",
            "..",
            "..",
            "..",
            "MCP_Rhino.Bridge",
            "bin",
            "Release",
            "net8.0",
            "MCP_Rhino.Bridge.exe"));
        yield return Path.GetFullPath(Path.Combine(
            pluginDirectory,
            "..",
            "..",
            "..",
            "..",
            "MCP_Rhino.Bridge",
            "bin",
            "Debug",
            "net8.0",
            "MCP_Rhino.Bridge.exe"));
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McNeel",
            "Rhinoceros",
            "8.0",
            "Plug-ins",
            "MCP_Rhino",
            "Bridge",
            "MCP_Rhino.Bridge.exe");

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Path.Combine(directory, "MCP_Rhino.Bridge.exe");
            }
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }
}
