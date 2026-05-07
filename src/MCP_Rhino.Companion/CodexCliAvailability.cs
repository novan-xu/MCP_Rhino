using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace MCP_Rhino.Companion;

public sealed record CodexCliAvailabilityResult(
    bool IsAvailable,
    string? ExecutablePath,
    Version? Version,
    string Message);

public static class CodexCliAvailability
{
    private static CodexCliAvailabilityResult? _cached;

    public static CodexCliAvailabilityResult Check()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        string? executablePath = FindCodexExecutable();
        if (executablePath is null)
        {
            _cached = new CodexCliAvailabilityResult(
                false,
                null,
                null,
                "Codex CLI was not found on PATH. Install via 'npm install -g @openai/codex' and run 'codex login'.");
            return _cached;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = CliProcessStartInfo.Create(executablePath, new[] { "--version" }, null);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            string versionText = string.IsNullOrWhiteSpace(output) ? error : output;
            Version? version = ParseVersion(versionText);

            _cached = new CodexCliAvailabilityResult(
                true,
                executablePath,
                version,
                version is not null
                    ? $"Codex CLI {version} is available."
                    : $"Codex CLI is available ({versionText.Trim()}).");
            return _cached;
        }
        catch (Exception ex)
        {
            _cached = new CodexCliAvailabilityResult(false, executablePath, null, ex.Message);
            return _cached;
        }
    }

    private static string? FindCodexExecutable()
    {
        string userLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "bin",
            "codex");

        foreach (string candidate in CandidateExecutablePaths("codex").Prepend(userLocal))
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
