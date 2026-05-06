using System.IO;

namespace MCP_Rhino.Companion;

public sealed record CompanionOptions(
    string DocumentPath,
    uint RuntimeSerial,
    string PipeName,
    string BridgePath,
    string? ModelId,
    bool ValidateOnly,
    bool Help)
{
    public static string Usage =>
        "Usage: MCP_Rhino.Companion.exe --document-path <path> --runtime-serial <serial> --pipe <pipe> --bridge <bridge.exe> [--model <id>] [--validate-args]";

    public static bool ContainsHelpFlag(IReadOnlyList<string> args)
    {
        return args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryParse(IReadOnlyList<string> args, out CompanionOptions? options, out string error)
    {
        options = null;
        error = string.Empty;

        bool help = ContainsHelpFlag(args);
        bool validateOnly = args.Any(arg => string.Equals(arg, "--validate-args", StringComparison.OrdinalIgnoreCase));
        string? documentPath = ReadOption(args, "--document-path");
        string? runtimeSerialText = ReadOption(args, "--runtime-serial");
        string? pipeName = ReadOption(args, "--pipe");
        string? bridgePath = ReadOption(args, "--bridge");
        string? modelId = ReadOption(args, "--model");

        if (help)
        {
            options = new CompanionOptions(string.Empty, 0, string.Empty, string.Empty, modelId, validateOnly, Help: true);
            return true;
        }

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            error = "--document-path is required.";
            return false;
        }

        if (!uint.TryParse(runtimeSerialText, out uint runtimeSerial) || runtimeSerial == 0)
        {
            error = "--runtime-serial must be a positive unsigned integer.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            error = "--pipe is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bridgePath))
        {
            error = "--bridge is required.";
            return false;
        }

        options = new CompanionOptions(
            Path.GetFullPath(documentPath),
            runtimeSerial,
            pipeName,
            Path.GetFullPath(bridgePath),
            modelId,
            validateOnly,
            Help: false);
        return true;
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                return i + 1 < args.Count ? args[i + 1] : null;
            }

            string prefix = name + "=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[prefix.Length..];
            }
        }

        return null;
    }
}
