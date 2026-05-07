using System.Text.RegularExpressions;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string RhinoChatSaveSafetySlug = "rhino-chat-save-safety-smoke-test";

    partial void RegisterRhinoChatSaveSafetyHandlers()
    {
        _extensionHandlers[RhinoChatSaveSafetySlug] = HandleRhinoChatSaveSafetySmokeTest;
    }

    private static bool HandleRhinoChatSaveSafetySmokeTest(string[] args)
    {
        try
        {
            string root = FindRepositoryRoot();
            string dispatcherPath = Path.Combine(
                root,
                "src",
                "MCP_Rhino.Server",
                "Infrastructure",
                "Plugin",
                "Panel",
                "PerDocumentPanelDispatcher.cs");

            string source = File.ReadAllText(dispatcherPath);
            string body = ExtractMethodBody(source, "OnEndSaveDocument");

            RequireSaveSafety(
                !body.Contains("TryStartDocument", StringComparison.Ordinal),
                "EndSaveDocument must not start or open the chat panel.");
            RequireSaveSafety(
                body.Contains("_host.UpdateDocumentPath(document)", StringComparison.Ordinal),
                "EndSaveDocument should only refresh existing document path state.");
            RequireSaveSafety(
                source.Contains("public bool TryShowDocument(RhinoDoc document)", StringComparison.Ordinal)
                    && source.Contains("return TryStartDocument(document);", StringComparison.Ordinal),
                "Explicit panel startup path should remain available for _Mcpchat.");

            Console.WriteLine("[OK] EndSaveDocument does not start the Claude Code panel/session.");
            Console.WriteLine("[OK] Explicit _Mcpchat panel startup path remains intact.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rhino chat save safety smoke failed: {ex}");
            Environment.ExitCode = 1;
            return true;
        }
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        Match match = Regex.Match(
            source,
            @"private\s+void\s+" + Regex.Escape(methodName) + @"\s*\([^)]*\)\s*\{",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidOperationException($"{methodName} was not found.");
        }

        int openBrace = source.IndexOf('{', match.Index);
        int depth = 0;
        for (int i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(openBrace + 1, i - openBrace - 1);
                }
            }
        }

        throw new InvalidOperationException($"{methodName} body was not closed.");
    }

    private static string FindRepositoryRoot()
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

    private static void RequireSaveSafety(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
