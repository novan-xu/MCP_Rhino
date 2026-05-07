namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

internal static class PanelChatWorkspace
{
    public static string GetWorkingDirectory(string pipeName)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "MCP_Rhino",
            SanitizeDirectoryName(pipeName),
            "workspace");

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string SanitizeDirectoryName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalid.Contains(ch) ? '_' : ch));
    }
}
