using System.IO;
using System.Text.Json;

namespace MCP_Rhino.Companion;

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
        string directory = GetTempDirectory(runtimeSerialNumber, pipeName);
        Directory.CreateDirectory(directory);

        string configPath = Path.Combine(directory, ".mcp.json");
        File.WriteAllText(configPath, BuildJson(bridgeExecutablePath, pipeName));
        return configPath;
    }

    public static string GetTempDirectory(uint runtimeSerialNumber)
    {
        return Path.Combine(Path.GetTempPath(), "MCP_Rhino", runtimeSerialNumber.ToString());
    }

    public static string GetTempDirectory(uint runtimeSerialNumber, string pipeName)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "MCP_Rhino",
            SanitizeDirectoryName(pipeName));
    }

    public static void DeleteTempDirectory(uint runtimeSerialNumber)
    {
        string directory = GetTempDirectory(runtimeSerialNumber);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    public static void DeleteTempDirectory(uint runtimeSerialNumber, string pipeName)
    {
        string directory = GetTempDirectory(runtimeSerialNumber, pipeName);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static string SanitizeDirectoryName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
