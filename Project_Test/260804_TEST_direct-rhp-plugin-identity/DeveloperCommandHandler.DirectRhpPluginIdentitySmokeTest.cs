using System.Reflection;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string DirectRhpPluginIdentitySlug = "direct-rhp-plugin-identity-smoke-test";

    partial void RegisterDirectRhpPluginIdentityHandlers()
    {
        _extensionHandlers[DirectRhpPluginIdentitySlug] = HandleDirectRhpPluginIdentitySmokeTest;
    }

    private static bool HandleDirectRhpPluginIdentitySmokeTest(string[] args)
    {
        try
        {
            string currentAssemblyPath = typeof(DeveloperCommandHandler).Assembly.Location;
            string pluginDirectory;
            if (string.Equals(Path.GetExtension(currentAssemblyPath), ".rhp", StringComparison.OrdinalIgnoreCase))
            {
                pluginDirectory = Path.GetDirectoryName(currentAssemblyPath)
                    ?? throw new InvalidOperationException("Unable to determine the loaded plug-in directory.");
            }
            else if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
            {
                pluginDirectory = Path.GetFullPath(args[1]);
            }
            else
            {
                throw new ArgumentException(
                    "CLI usage: direct-rhp-plugin-identity-smoke-test <bundle-Plugin-directory>");
            }

            string rhpPath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.rhp");
            string duplicateDllPath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.dll");
            string duplicateExePath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.exe");
            string runtimePath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.Runtime.dll");
            string rhpDepsPath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.deps.json");
            string runtimeDepsPath = Path.Combine(pluginDirectory, "MCP_Rhino.Server.Runtime.deps.json");

            RequireDirectRhpIdentity(File.Exists(rhpPath), "MCP_Rhino.Server.rhp is missing.");
            RequireDirectRhpIdentity(!File.Exists(duplicateDllPath), "Duplicate MCP_Rhino.Server.dll is present.");
            RequireDirectRhpIdentity(!File.Exists(duplicateExePath), "MCP_Rhino.Server.exe must not be packaged.");
            RequireDirectRhpIdentity(File.Exists(runtimePath), "MCP_Rhino.Server.Runtime.dll is missing.");
            RequireDirectRhpIdentity(File.Exists(rhpDepsPath), "MCP_Rhino.Server.deps.json is missing.");
            RequireDirectRhpIdentity(File.Exists(runtimeDepsPath), "MCP_Rhino.Server.Runtime.deps.json is missing.");

            string? rhpAssemblyName = AssemblyName.GetAssemblyName(rhpPath).Name;
            string? runtimeAssemblyName = AssemblyName.GetAssemblyName(runtimePath).Name;
            RequireDirectRhpIdentity(
                string.Equals(rhpAssemblyName, "MCP_Rhino.Server", StringComparison.Ordinal),
                $"Unexpected RHP assembly identity: {rhpAssemblyName ?? "<null>"}.");
            RequireDirectRhpIdentity(
                string.Equals(runtimeAssemblyName, "MCP_Rhino.Server.Runtime", StringComparison.Ordinal),
                $"Unexpected isolated runtime identity: {runtimeAssemblyName ?? "<null>"}.");

            string rhpDeps = File.ReadAllText(rhpDepsPath);
            string runtimeDeps = File.ReadAllText(runtimeDepsPath);
            RequireDirectRhpIdentity(
                rhpDeps.Contains("\"MCP_Rhino.Server.rhp\"", StringComparison.Ordinal),
                "RHP dependency metadata does not name MCP_Rhino.Server.rhp as its runtime asset.");
            RequireDirectRhpIdentity(
                !rhpDeps.Contains("\"MCP_Rhino.Server.dll\"", StringComparison.Ordinal),
                "RHP dependency metadata still names MCP_Rhino.Server.dll.");
            RequireDirectRhpIdentity(
                runtimeDeps.Contains("\"MCP_Rhino.Server.Runtime.dll\"", StringComparison.Ordinal),
                "Isolated runtime dependency metadata does not name MCP_Rhino.Server.Runtime.dll.");

            Console.WriteLine(
                $"[OK] {DirectRhpPluginIdentitySlug}|plugin={rhpAssemblyName}|runtime={runtimeAssemblyName}|directory={pluginDirectory}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Direct RHP plug-in identity smoke failed: {ex.Message}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RequireDirectRhpIdentity(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
