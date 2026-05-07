using System.Diagnostics;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Companion;

internal static class CompanionProcessLauncher
{
    private const string CompanionExecutableName = "MCP_Rhino.Companion.exe";

    public static string? FindCompanionExecutable(string pluginDirectory)
    {
        foreach (string candidate in EnumerateCompanionCandidates(pluginDirectory))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static CompanionSessionHandle Start(CompanionLaunchSpec spec)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = spec.CompanionExecutablePath,
            UseShellExecute = true,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(spec.CompanionExecutablePath) ?? Environment.CurrentDirectory
        };

        startInfo.ArgumentList.Add("--document-path");
        startInfo.ArgumentList.Add(spec.DocumentPath);
        startInfo.ArgumentList.Add("--runtime-serial");
        startInfo.ArgumentList.Add(spec.RuntimeSerialNumber.ToString());
        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(spec.PipeName);
        startInfo.ArgumentList.Add("--bridge");
        startInfo.ArgumentList.Add(spec.BridgeExecutablePath);

        if (!string.IsNullOrWhiteSpace(spec.ModelId))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(spec.ModelId);
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        process.Start();
        return new CompanionSessionHandle(spec, process);
    }

    private static IEnumerable<string> EnumerateCompanionCandidates(string pluginDirectory)
    {
        yield return Path.Combine(pluginDirectory, CompanionExecutableName);
        yield return Path.GetFullPath(Path.Combine(
            pluginDirectory,
            "..",
            "..",
            "..",
            "..",
            "MCP_Rhino.Companion",
            "bin",
            "Release",
            "net8.0-windows",
            CompanionExecutableName));
        yield return Path.GetFullPath(Path.Combine(
            pluginDirectory,
            "..",
            "..",
            "..",
            "..",
            "MCP_Rhino.Companion",
            "bin",
            "Debug",
            "net8.0-windows",
            CompanionExecutableName));
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "McNeel",
            "Rhinoceros",
            "8.0",
            "Plug-ins",
            "MCP_Rhino",
            "Companion",
            CompanionExecutableName);

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return Path.Combine(directory, CompanionExecutableName);
            }
        }
    }
}
