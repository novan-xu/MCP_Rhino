using System.Reflection;
using System.Text.Json;
using PanelCladdingEditor.Application.Services.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingHideSmokeCommandSmoke;

internal static class Program
{
    private static readonly string[] ExpectedCommandTypes =
    {
        "PanelCladdingClearCommand",
        "PanelCladdingCreateCommand",
        "PanelCladdingCurveTemplateCommand",
        "PanelCladdingEditorCommand",
        "PanelCladdingMatchCommand",
        "PanelCladdingMatchCurveCommand",
        "PanelCladdingSpawnCrvCommand",
        "PanelCladdingSpawnSrfCommand",
        "PanelCladdingSyncCrvCommand",
        "PanelCladdingSyncSrfCommand"
    };

    private static int Main()
    {
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            VerifyCompiledCommandSurface();
            VerifyProductionSource(repositoryRoot);
            VerifyPackageSurface(repositoryRoot);

            Console.WriteLine("[OK] PanelCladdingEditor exposes exactly ten production Rhino commands.");
            Console.WriteLine("[OK] PCEditorSmoke is absent from source, compiled types, metadata, and package documentation.");
            Console.WriteLine("[OK] Internal standalone tests remain external to Rhino's command table.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Panel cladding smoke-command retirement test failed: {ex}");
            return 1;
        }
    }

    private static void VerifyCompiledCommandSurface()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Require(commands.Select(type => type.Name).SequenceEqual(ExpectedCommandTypes),
            $"Unexpected Rhino command type set: {string.Join(", ", commands.Select(type => type.Name))}");
        Require(assembly.GetType("PanelCladdingEditor.UI.PanelCladdingEditorSmokeCommand") is null,
            "PanelCladdingEditorSmokeCommand remains compiled into the production assembly.");
        Require(commands.All(type => type.GUID != Guid.Empty),
            "Every remaining Rhino command must retain an explicit non-empty GUID.");
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "Every remaining Rhino command must retain a unique GUID.");
    }

    private static void VerifyProductionSource(string repositoryRoot)
    {
        string uiRoot = Path.Combine(repositoryRoot, "src", "PanelCladdingEditor", "UI");
        string retiredSource = Path.Combine(uiRoot, "PanelCladdingEditorSmokeCommand.cs");
        Require(!File.Exists(retiredSource),
            "The retired production smoke-command source file still exists.");

        string commandSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(uiRoot, "*Command.cs").Select(File.ReadAllText));
        Require(!commandSource.Contains("PCEditorSmoke", StringComparison.OrdinalIgnoreCase),
            "A production command source still exposes PCEditorSmoke.");
    }

    private static void VerifyPackageSurface(string repositoryRoot)
    {
        string packageRoot = Path.Combine(repositoryRoot, "Packaging", "PanelCladdingEditor");
        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(packageRoot, "package-manifest.json")));
        Require(manifest.RootElement.GetProperty("command").GetString() == "PCEditor",
            "PCEditor must remain the primary package command.");
        Require(!manifest.RootElement.TryGetProperty("smokeCommand", out _),
            "Package metadata still exposes a smokeCommand.");

        string readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
        Require(!readme.Contains("PCEditorSmoke", StringComparison.OrdinalIgnoreCase),
            "Package documentation still advertises PCEditorSmoke.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "PanelCladdingEditor")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the MCP_Rhino repository root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
