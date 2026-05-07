using System.IO;

namespace MCP_Rhino.Companion;

internal static class RuntimePolicyPrompt
{
    private const string FileName = "McpRhinoRuntimePolicyBundle.md";

    public static string Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Prompts", "Runtime", FileName);
        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        return "MCP_Rhino runtime policy file was not found. Use only available runtime tools and report missing capabilities.";
    }
}
