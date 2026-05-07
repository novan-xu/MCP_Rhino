using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MCP_Rhino.Server.Infrastructure.ClaudeCode;

public sealed record ClaudeCodeAvailabilityResult(
    bool IsAvailable,
    string? ExecutablePath,
    Version? Version,
    string Message);

public static class ClaudeCodeAvailability
{
    private static readonly Version MinimumVersion = new(2, 1, 119);
    private static ClaudeCodeAvailabilityResult? _cached;

    public static ClaudeCodeAvailabilityResult Check()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        string? executablePath = FindClaudeExecutable();
        if (executablePath is null)
        {
            _cached = new ClaudeCodeAvailabilityResult(
                false,
                null,
                null,
                "Claude Code was not found on PATH.");
            return _cached;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = CreateStartInfo(executablePath, new[] { "--version" }, null);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            string versionText = string.IsNullOrWhiteSpace(output) ? error : output;
            Version? version = ParseVersion(versionText);
            if (version is null)
            {
                _cached = new ClaudeCodeAvailabilityResult(
                    false,
                    executablePath,
                    null,
                    $"Claude Code version could not be parsed: {versionText.Trim()}");
                return _cached;
            }

            if (version < MinimumVersion)
            {
                _cached = new ClaudeCodeAvailabilityResult(
                    false,
                    executablePath,
                    version,
                    $"Claude Code {version} is below the required {MinimumVersion}.");
                return _cached;
            }

            _cached = new ClaudeCodeAvailabilityResult(
                true,
                executablePath,
                version,
                $"Claude Code {version} is available.");
            return _cached;
        }
        catch (Exception ex)
        {
            _cached = new ClaudeCodeAvailabilityResult(false, executablePath, null, ex.Message);
            return _cached;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string executablePath, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        return CliProcessStartInfo.Create(executablePath, arguments, workingDirectory);
    }

    private static string? FindClaudeExecutable()
    {
        string userLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "bin",
            "claude");

        foreach (string candidate in CandidateExecutablePaths("claude").Prepend(userLocal))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateExecutablePaths(string commandName)
    {
        string[] extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                yield return Path.Combine(directory, commandName + extension);
            }
        }
    }

    private static Version? ParseVersion(string text)
    {
        Match match = Regex.Match(text, @"(?<version>\d+\.\d+\.\d+)");
        return match.Success && Version.TryParse(match.Groups["version"].Value, out Version? version)
            ? version
            : null;
    }

}
