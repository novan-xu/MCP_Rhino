namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string DebugBridgeOnlyPluginSlug = "debug-bridge-only-plugin-smoke-test";

    partial void RegisterDebugBridgeOnlyPluginHandlers()
    {
        _extensionHandlers[DebugBridgeOnlyPluginSlug] = HandleDebugBridgeOnlyPluginSmokeTest;
    }

    private bool HandleDebugBridgeOnlyPluginSmokeTest(string[] args)
    {
        try
        {
            RunDebugBridgeOnlyPluginSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Debug bridge-only plugin smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunDebugBridgeOnlyPluginSmoke()
    {
        string root = FindDebugBridgeOnlyRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "MCP_Rhino.Server", "MCP_Rhino.Server.csproj");
        string pluginSourcePath = Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "Plugin", "MCP_Rhino.RhinoPlugin.cs");
        string debugRhpPath = Path.Combine(root, "src", "MCP_Rhino.Server", "bin", "Debug", "net8.0", "MCP_Rhino.Server.rhp");
        string releaseRhpPath = Path.Combine(root, "src", "MCP_Rhino.Server", "bin", "Release", "net8.0", "MCP_Rhino.Server.rhp");

        string project = File.ReadAllText(projectPath);
        RequireDebugBridgeOnly(project.Contains("MCP_RHINO_BRIDGE_PIPE_ONLY", StringComparison.Ordinal),
            "Debug build defines MCP_RHINO_BRIDGE_PIPE_ONLY.");
        RequireDebugBridgeOnly(project.Contains("CopyRhinoPluginAssembly", StringComparison.Ordinal),
            "Server project copies every built assembly to an .rhp plugin.");

        string pluginSource = File.ReadAllText(pluginSourcePath);
        RequireDebugBridgeOnly(pluginSource.Contains("IsBridgePipeOnlyBuild", StringComparison.Ordinal),
            "Plugin has an explicit bridge-only build mode.");
        RequireDebugBridgeOnly(pluginSource.Contains("Debug bridge-only plugin does not start panel-bound MCP pipes", StringComparison.Ordinal),
            "Debug plugin blocks panel-bound pipe startup.");
        RequireDebugBridgeOnly(pluginSource.Contains("This Debug plugin is bridge-pipe-only", StringComparison.Ordinal),
            "Debug plugin blocks _Mcpchat and directs users to the bridge pipe.");
        RequireDebugBridgeOnly(pluginSource.Contains("TryShowCompanion", StringComparison.Ordinal),
            "Release plugin retains Companion/chat path in shared source.");

        RequireExistingPlugin(debugRhpPath, "Debug plugin .rhp exists.");
        RequireExistingPlugin(releaseRhpPath, "Release plugin .rhp exists.");

        RequireAssemblyContains(debugRhpPath, "Debug bridge-only plugin loaded", "Debug .rhp contains bridge-only marker.");
        RequireAssemblyContains(debugRhpPath, "Developer debug pipe requested", "Debug .rhp retains developer debug pipe startup.");
        RequireAssemblyContains(releaseRhpPath, "MCP_Rhino panel pipes are process-scoped", "Release .rhp contains chat-panel marker.");
        RequireAssemblyContains(releaseRhpPath, "Developer debug pipe requested", "Release .rhp retains developer debug pipe startup.");

        Console.WriteLine("[OK] Debug .rhp is built and marked bridge-pipe-only.");
        Console.WriteLine("[OK] Release .rhp is built and remains chat-capable.");
        Console.WriteLine("[OK] Debug and Release both retain the Developer Debug Control Path marker.");
    }

    private static string FindDebugBridgeOnlyRepositoryRoot()
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

    private static void RequireExistingPlugin(string path, string message)
    {
        RequireDebugBridgeOnly(File.Exists(path), $"{message} Missing: {path}");
        RequireDebugBridgeOnly(new FileInfo(path).Length > 0, $"{message} File is empty: {path}");
    }

    private static void RequireAssemblyContains(string assemblyPath, string marker, string message)
    {
        byte[] bytes = File.ReadAllBytes(assemblyPath);
        byte[] markerBytes = System.Text.Encoding.Unicode.GetBytes(marker);
        RequireDebugBridgeOnly(IndexOf(bytes, markerBytes) >= 0, message);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool matched = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static void RequireDebugBridgeOnly(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
