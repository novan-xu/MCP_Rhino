namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string RhinoChatFileLockSafetySlug = "rhino-chat-file-lock-safety-smoke-test";

    partial void RegisterRhinoChatFileLockSafetyHandlers()
    {
        _extensionHandlers[RhinoChatFileLockSafetySlug] = HandleRhinoChatFileLockSafetySmokeTest;
    }

    private static bool HandleRhinoChatFileLockSafetySmokeTest(string[] args)
    {
        try
        {
            string root = FindRepositoryRoot();
            string companionClaude = ReadRepoFile(root, "src", "MCP_Rhino.Companion", "ClaudeCodeSession.cs");
            string companionCodex = ReadRepoFile(root, "src", "MCP_Rhino.Companion", "CodexCliSession.cs");
            string companionWorkspace = ReadRepoFile(root, "src", "MCP_Rhino.Companion", "CompanionWorkspace.cs");
            string companionLauncher = ReadRepoFile(root, "src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "Companion", "CompanionProcessLauncher.cs");
            string panelSession = ReadRepoFile(root, "src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "Panel", "PanelChatSessionService.cs");
            string panelWorkspace = ReadRepoFile(root, "src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "Panel", "PanelChatWorkspace.cs");
            string applier = ReadRepoFile(root, "src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveRhinoObjectEditOperationApplier.cs");
            string geometryMutator = ReadRepoFile(root, "src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveRhinoGeometryMutator.cs");

            RequireFileLockSafety(
                companionClaude.Contains("CompanionWorkspace.GetWorkingDirectory(_options.PipeName)", StringComparison.Ordinal)
                    && !companionClaude.Contains("Path.GetDirectoryName(_options.DocumentPath)", StringComparison.Ordinal),
                "Companion Claude session must not use the Rhino document folder as working directory.");
            RequireFileLockSafety(
                companionCodex.Contains("CompanionWorkspace.GetWorkingDirectory(_options.PipeName)", StringComparison.Ordinal)
                    && !companionCodex.Contains("Path.GetDirectoryName(_options.DocumentPath)", StringComparison.Ordinal),
                "Companion Codex session must not use the Rhino document folder as working directory.");
            RequireFileLockSafety(
                panelSession.Contains("PanelChatWorkspace.GetWorkingDirectory(PipeName)", StringComparison.Ordinal)
                    && !panelSession.Contains("Path.GetDirectoryName(DocumentPath)", StringComparison.Ordinal),
                "Legacy panel Claude session must not use the Rhino document folder as working directory.");
            RequireFileLockSafety(
                companionWorkspace.Contains("GetWorkingDirectory(string pipeName)", StringComparison.Ordinal)
                    && companionWorkspace.Contains("Directory.CreateDirectory(directory)", StringComparison.Ordinal)
                    && panelWorkspace.Contains("GetWorkingDirectory(string pipeName)", StringComparison.Ordinal),
                "Panel and companion workspace helpers must create isolated per-pipe working directories.");
            RequireFileLockSafety(
                companionLauncher.Contains("UseShellExecute = true", StringComparison.Ordinal)
                    && !companionLauncher.Contains("UseShellExecute = false", StringComparison.Ordinal),
                "Rhino must launch the companion through ShellExecute so Rhino document file handles are not inherited.");
            RequireFileLockSafety(
                companionClaude.Contains("\"--no-session-persistence\"", StringComparison.Ordinal)
                    && companionClaude.Contains("\"--setting-sources\"", StringComparison.Ordinal)
                    && companionClaude.Contains("\"user\"", StringComparison.Ordinal)
                    && companionClaude.Contains("\"--tools\"", StringComparison.Ordinal)
                    && companionClaude.Contains("string.Empty", StringComparison.Ordinal),
                "Companion Claude launch must disable local project tooling.");
            RequireFileLockSafety(
                panelSession.Contains("\"--no-session-persistence\"", StringComparison.Ordinal)
                    && panelSession.Contains("\"--setting-sources\"", StringComparison.Ordinal)
                    && panelSession.Contains("\"user\"", StringComparison.Ordinal)
                    && panelSession.Contains("\"--tools\"", StringComparison.Ordinal)
                    && panelSession.Contains("string.Empty", StringComparison.Ordinal),
                "Legacy panel Claude launch must disable local project tooling.");
            RequireFileLockSafety(
                companionClaude.Contains("Do not use local shell commands or direct .3dm file reads", StringComparison.Ordinal)
                    && panelSession.Contains("Do not use local shell commands or direct .3dm file reads", StringComparison.Ordinal),
                "Claude prompts must forbid local shell/direct .3dm fallback.");
            RequireFileLockSafety(
                applier.Contains("RhinoDoc document", StringComparison.Ordinal)
                    && !applier.Contains("RhinoDoc.ActiveDoc", StringComparison.Ordinal),
                "Object edit applier must use the resolved bound RhinoDoc, not RhinoDoc.ActiveDoc.");
            RequireFileLockSafety(
                geometryMutator.Contains("RhinoDoc document", StringComparison.Ordinal)
                    && !geometryMutator.Contains("RhinoDoc.ActiveDoc", StringComparison.Ordinal),
                "Geometry mutator must use the resolved bound RhinoDoc, not RhinoDoc.ActiveDoc.");

            Console.WriteLine("[OK] Rhino launches the companion without inherited document file handles.");
            Console.WriteLine("[OK] Claude/Codex sessions use isolated per-pipe working directories.");
            Console.WriteLine("[OK] Claude local project/file tools are disabled for panel sessions.");
            Console.WriteLine("[OK] Claude prompts forbid local shell/direct .3dm fallback.");
            Console.WriteLine("[OK] Object edit and geometry mutators use the resolved bound RhinoDoc.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rhino chat file lock safety smoke failed: {ex}");
            Environment.ExitCode = 1;
            return true;
        }
    }

    private static string ReadRepoFile(string root, params string[] parts)
    {
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
    }

    private static void RequireFileLockSafety(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
