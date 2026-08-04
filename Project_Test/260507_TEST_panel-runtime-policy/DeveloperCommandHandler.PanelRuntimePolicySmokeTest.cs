namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string PanelRuntimePolicySlug = "panel-runtime-policy-smoke-test";
    private const string RuntimePolicyFileName = "McpRhinoRuntimePolicyBundle.md";

    partial void RegisterPanelRuntimePolicyHandlers()
    {
        _extensionHandlers[PanelRuntimePolicySlug] = HandlePanelRuntimePolicySmokeTest;
    }

    private bool HandlePanelRuntimePolicySmokeTest(string[] args)
    {
        try
        {
            RunPanelRuntimePolicySmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Panel runtime policy smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunPanelRuntimePolicySmoke()
    {
        string serverPolicyPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "Runtime", RuntimePolicyFileName);
        RequirePanelRuntimePolicy(File.Exists(serverPolicyPath), $"Server runtime policy file was not copied: {serverPolicyPath}");

        string policy = File.ReadAllText(serverPolicyPath);
        RequirePanelRuntimePolicy(policy.Contains("MCP_Rhino Runtime Policy Bundle", StringComparison.Ordinal), "Policy title missing.");
        RequirePanelRuntimePolicy(policy.Contains("External File Access", StringComparison.Ordinal), "External file policy missing.");
        RequirePanelRuntimePolicy(policy.Contains("Spreadsheet And Slide Work", StringComparison.Ordinal), "Spreadsheet and slide policy missing.");
        RequirePanelRuntimePolicy(policy.Contains("Do not perform repository maintenance", StringComparison.Ordinal), "Construction boundary missing.");
        RequirePanelRuntimePolicy(policy.All(ch => ch <= sbyte.MaxValue), "Policy should stay ASCII / English-only.");

        string root = FindPanelRuntimePolicyRepositoryRoot();
        string configuration = FindBuildConfigurationName();
        string companionPolicyPath = Path.Combine(
            root,
            "src",
            "MCP_Rhino.Companion",
            "bin",
            configuration,
            "net8.0-windows",
            "Prompts",
            "Runtime",
            RuntimePolicyFileName);
        RequirePanelRuntimePolicy(File.Exists(companionPolicyPath), $"Companion runtime policy file was not copied: {companionPolicyPath}");

        string companionPolicy = File.ReadAllText(companionPolicyPath);
        RequirePanelRuntimePolicy(string.Equals(policy, companionPolicy, StringComparison.Ordinal), "Server and Companion policy copies differ.");

        string companionClaude = File.ReadAllText(Path.Combine(root, "src", "MCP_Rhino.Companion", "ClaudeCodeSession.cs"));
        string companionCodex = File.ReadAllText(Path.Combine(root, "src", "MCP_Rhino.Companion", "CodexCliSession.cs"));
        string fallbackPanel = File.ReadAllText(Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "Panel", "PanelChatSessionService.cs"));
        RequirePanelRuntimePolicy(companionClaude.Contains("RuntimePolicyPrompt.Load()", StringComparison.Ordinal), "Claude Companion prompt does not load runtime policy.");
        RequirePanelRuntimePolicy(companionCodex.Contains("RuntimePolicyPrompt.Load()", StringComparison.Ordinal), "Codex Companion prompt does not load runtime policy.");
        RequirePanelRuntimePolicy(fallbackPanel.Contains("RuntimePolicyPrompt.Load()", StringComparison.Ordinal), "Fallback panel prompt does not load runtime policy.");
        RequireProjectWorkspaceRouteDoesNotLoadRuntimePolicy(root);

        Console.WriteLine("[OK] Runtime policy bundle copied to Server output.");
        Console.WriteLine("[OK] Runtime policy bundle copied to Companion output.");
        Console.WriteLine("[OK] Companion and fallback panel prompts load the runtime policy.");
        Console.WriteLine("[OK] Project workspace and debug-pipe routes do not inject the panel runtime policy.");
    }

    private static void RequireProjectWorkspaceRouteDoesNotLoadRuntimePolicy(string root)
    {
        string[] nonPanelRouteFiles =
        {
            Path.Combine(root, "src", "MCP_Rhino.Server", "Program.cs"),
            Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "CLI", "DeveloperCommandHandler.cs"),
            Path.Combine(root, "src", "MCP_Rhino.Bridge", "Program.cs")
        };

        foreach (string path in nonPanelRouteFiles)
        {
            string source = File.ReadAllText(path);
            RequirePanelRuntimePolicy(!source.Contains("RuntimePolicyPrompt.Load()", StringComparison.Ordinal),
                $"Non-panel route loads runtime policy: {Path.GetRelativePath(root, path)}");
            RequirePanelRuntimePolicy(!source.Contains(RuntimePolicyFileName, StringComparison.Ordinal),
                $"Non-panel route references runtime policy bundle: {Path.GetRelativePath(root, path)}");
        }
    }

    private static string FindPanelRuntimePolicyRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MCP_Rhino.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from " + AppContext.BaseDirectory);
    }

    private static string FindBuildConfigurationName()
    {
        DirectoryInfo baseDirectory = new(AppContext.BaseDirectory);
        string? configuration = baseDirectory.Parent?.Name;
        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new InvalidOperationException("Could not resolve build configuration from " + AppContext.BaseDirectory);
        }

        return configuration;
    }

    private static void RequirePanelRuntimePolicy(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
