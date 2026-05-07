namespace MCP_Rhino.Companion;

internal static class AgentCliCatalog
{
    public const string ClaudeCode = "claude code cli";
    public const string Codex = "codex cli";
    public const string DefaultModel = "Default";

    private static readonly string[] ClaudeModels =
    {
        DefaultModel,
        "claude-haiku-4-5",
        "claude-sonnet-4-6",
        "claude-opus-4-7"
    };

    private static readonly string[] CodexModels =
    {
        DefaultModel,
        "gpt-5.5",
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.3-codex",
        "gpt-5.3-codex-spark",
        "gpt-5.2"
    };

    public static string NormalizeCli(string? cli)
    {
        return string.Equals(cli, Codex, StringComparison.OrdinalIgnoreCase)
            ? Codex
            : ClaudeCode;
    }

    public static IReadOnlyList<string> ModelsFor(string cli, string? selectedModel)
    {
        string normalized = NormalizeCli(cli);
        string[] baseModels = string.Equals(normalized, Codex, StringComparison.OrdinalIgnoreCase)
            ? CodexModels
            : ClaudeModels;

        if (string.IsNullOrWhiteSpace(selectedModel)
            || baseModels.Any(model => string.Equals(model, selectedModel, StringComparison.OrdinalIgnoreCase)))
        {
            return baseModels;
        }

        return baseModels.Concat(new[] { selectedModel }).ToArray();
    }

    public static string? NormalizeModel(string? model)
    {
        return string.IsNullOrWhiteSpace(model)
            || string.Equals(model, DefaultModel, StringComparison.OrdinalIgnoreCase)
            ? null
            : model.Trim();
    }
}
