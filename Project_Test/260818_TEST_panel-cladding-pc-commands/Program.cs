using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PanelCladdingEditor.Application.Services.PanelCladding;
using Rhino.Commands;

namespace PanelCladdingPcCommandsSmoke;

internal static class Program
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedCommands =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PanelCladdingEditorCommand"] = "PCEditor",
            ["PanelCladdingCreateCommand"] = "PCCreate",
            ["PanelCladdingClearCommand"] = "PCClear",
            ["PanelCladdingCurveTemplateCommand"] = "PCCrvTemplate",
            ["PanelCladdingMatchCommand"] = "PCMatchSrf",
            ["PanelCladdingMatchCurveCommand"] = "PCMatchCrv",
            ["PanelCladdingSpawnCrvCommand"] = "PCSpawnCrv",
            ["PanelCladdingSpawnSrfCommand"] = "PCSpawnSrf",
            ["PanelCladdingSyncCrvCommand"] = "PCSyncCrv",
            ["PanelCladdingSyncSrfCommand"] = "PCSyncSrf",
            ["PanelCladdingUpdateCommand"] = "PCUpdate",
            ["PanelCladdingPidCommand"] = "PCpid"
        };

    private static int Main()
    {
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            VerifyAssemblyCommandTypes();
            VerifyRegisteredNames(repositoryRoot);
            VerifyPackageMetadata(repositoryRoot);

            Console.WriteLine("[OK] All twelve user-facing PanelCladdingEditor Rhino commands use the PC prefix.");
            Console.WriteLine("[OK] No legacy PanelCladding-prefixed command alias remains registered.");
            Console.WriteLine("[OK] Command GUIDs remain explicit and unique, and package metadata names PCEditor only.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Panel cladding PC command smoke failed: {ex}");
            return 1;
        }
    }

    private static void VerifyAssemblyCommandTypes()
    {
        Assembly assembly = typeof(PanelCladdingKeyService).Assembly;
        Type[] commands = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Command).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Require(commands.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(ExpectedCommands.Keys.OrderBy(name => name, StringComparer.Ordinal)),
            $"Unexpected Rhino command type set: {string.Join(", ", commands.Select(type => type.Name))}");
        Require(commands.All(type => type.GUID != Guid.Empty),
            "Every Rhino command must retain an explicit non-empty GUID.");
        Require(commands.Select(type => type.GUID).Distinct().Count() == commands.Length,
            "Every Rhino command must retain a unique GUID.");
    }

    private static void VerifyRegisteredNames(string repositoryRoot)
    {
        string commandRoot = Path.Combine(repositoryRoot, "src", "PanelCladdingEditor", "UI");
        var actualNames = new List<string>();
        foreach ((string commandType, string expectedName) in ExpectedCommands)
        {
            string path = Path.Combine(commandRoot, $"{commandType}.cs");
            string source = File.ReadAllText(path);
            Match match = Regex.Match(
                source,
                "public\\s+override\\s+string\\s+EnglishName\\s*=>\\s*\\\"([^\\\"]+)\\\"\\s*;",
                RegexOptions.CultureInvariant);
            Require(match.Success, $"{commandType} does not declare a literal EnglishName.");

            string actualName = match.Groups[1].Value;
            actualNames.Add(actualName);
            Require(actualName == expectedName,
                $"{commandType} registers '{actualName}' instead of '{expectedName}'.");
            Require(actualName.StartsWith("PC", StringComparison.Ordinal),
                $"{actualName} does not use the PC prefix.");
            Require(!actualName.StartsWith("PanelCladding", StringComparison.OrdinalIgnoreCase),
                $"Legacy command prefix remains registered as {actualName}.");

            foreach (Match writeCall in Regex.Matches(
                         source,
                         "RhinoApp\\.WriteLine\\((?<body>.*?)\\);",
                         RegexOptions.CultureInvariant | RegexOptions.Singleline))
            {
                string body = writeCall.Groups["body"].Value;
                bool containsLegacyCommandPrefix =
                    body.Contains("PanelCladding", StringComparison.OrdinalIgnoreCase);
                bool isProductIdentityMessage = body.Contains(
                    "PanelCladdingEditor is a standalone Rhino plug-in",
                    StringComparison.Ordinal);
                Require(!containsLegacyCommandPrefix || isProductIdentityMessage,
                    $"{commandType} retains a legacy command name in Rhino command-line output.");
            }
        }

        Require(actualNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ExpectedCommands.Count,
            "Registered Rhino command names must be unique without regard to case.");
    }

    private static void VerifyPackageMetadata(string repositoryRoot)
    {
        string manifestPath = Path.Combine(
            repositoryRoot, "Packaging", "PanelCladdingEditor", "package-manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = manifest.RootElement;
        Require(root.GetProperty("name").GetString() == "PanelCladdingEditor",
            "The product identity must remain PanelCladdingEditor.");
        Require(root.GetProperty("command").GetString() == "PCEditor",
            "Package metadata must name PCEditor as the primary command.");
        Require(!root.TryGetProperty("smokeCommand", out _),
            "Package metadata must not expose a production smoke command.");
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
