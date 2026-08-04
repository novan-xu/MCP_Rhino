using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Tools.Editing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string BulkAttributeRecipesSlug = "bulk-attribute-recipes-smoke-test";

    partial void RegisterBulkAttributeRecipesHandlers()
    {
        _extensionHandlers[BulkAttributeRecipesSlug] = HandleBulkAttributeRecipesSmokeTest;
    }

    private bool HandleBulkAttributeRecipesSmokeTest(string[] args)
    {
        try
        {
            RunBulkAttributeRecipesSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Bulk attribute recipes smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunBulkAttributeRecipesSmoke()
    {
        VerifyToolMethod(
            typeof(PreviewBulkObjectAttributeRecipeTool),
            "PreviewBulkObjectAttributeRecipe",
            readOnly: true,
            destructive: false,
            openWorld: false,
            expectedDescriptionFragments: new[]
            {
                "compact bulk object attribute recipes",
                "without mutating",
                "avoid large per-object payloads"
            });

        VerifyToolMethod(
            typeof(ApplyBulkObjectAttributeRecipeTool),
            "ApplyBulkObjectAttributeRecipe",
            readOnly: false,
            destructive: false,
            openWorld: false,
            expectedDescriptionFragments: new[]
            {
                "compact bulk object attribute recipes",
                "server-side",
                "one Rhino undo record"
            });

        VerifyRequestContracts();
        VerifySourceContracts();

        Console.WriteLine("[OK] Bulk attribute recipe MCP tools expose preview/apply with explicit safety metadata.");
        Console.WriteLine("[OK] Bulk attribute recipe DTOs and source contracts support compact template-driven updates.");
    }

    private static void VerifyToolMethod(
        Type toolType,
        string methodName,
        bool readOnly,
        bool destructive,
        bool openWorld,
        IReadOnlyList<string> expectedDescriptionFragments)
    {
        RequireBulkAttributeRecipe(toolType.GetCustomAttribute<McpServerToolTypeAttribute>() is not null, $"{toolType.Name} is missing [McpServerToolType].");

        MethodInfo? method = toolType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        RequireBulkAttributeRecipe(method is not null, $"Tool method was not found: {toolType.Name}.{methodName}");
        MethodInfo methodInfo = method!;

        McpServerToolAttribute? tool = methodInfo.GetCustomAttribute<McpServerToolAttribute>();
        RequireBulkAttributeRecipe(tool is not null, $"{methodName} is missing [McpServerTool].");
        RequireBulkAttributeRecipe(tool!.ReadOnly == readOnly, $"{methodName} ReadOnly should be {readOnly}.");
        RequireBulkAttributeRecipe(tool.Destructive == destructive, $"{methodName} Destructive should be {destructive}.");
        RequireBulkAttributeRecipe(tool.OpenWorld == openWorld, $"{methodName} OpenWorld should be {openWorld}.");

        string description = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        foreach (string fragment in expectedDescriptionFragments)
        {
            RequireBulkAttributeRecipe(
                description.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"{methodName} description should include [{fragment}].");
        }

        HashSet<string> parameterNames = methodInfo.GetParameters()
            .Select(parameter => parameter.Name ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string parameterName in new[]
        {
            "filePath",
            "userTextWrites",
            "removeUserTextKeys",
            "targetLayerFullPath",
            "displayColor",
            "objectNameTemplate",
            "layerQueries",
            "confirmedLayerFullPaths",
            "objectTypes",
            "userAttributeConditions",
            "matchMode",
            "userAttributeMatchMode"
        })
        {
            RequireBulkAttributeRecipe(parameterNames.Contains(parameterName), $"{methodName} is missing parameter [{parameterName}].");
        }
    }

    private static void VerifyRequestContracts()
    {
        RequireBulkAttributeRecipe(
            typeof(PreviewBulkObjectAttributeRecipeRequest).BaseType == typeof(ObjectAttributeRecipeRequestBase),
            "Preview request should inherit the shared recipe request base.");

        RequireBulkAttributeRecipe(
            typeof(ApplyBulkObjectAttributeRecipeRequest).BaseType == typeof(ObjectAttributeRecipeRequestBase),
            "Apply request should inherit the shared recipe request base.");

        foreach (string propertyName in new[]
        {
            "FilePath",
            "LayerQueries",
            "ConfirmedLayerFullPaths",
            "ObjectTypes",
            "UserAttributeConditions",
            "MatchMode",
            "UserAttributeMatchMode",
            "UserTextWrites",
            "RemoveUserTextKeys",
            "TargetLayerFullPath",
            "DisplayColor",
            "ObjectNameTemplate"
        })
        {
            RequireBulkAttributeRecipe(
                typeof(ObjectAttributeRecipeRequestBase).GetProperty(propertyName) is not null,
                $"ObjectAttributeRecipeRequestBase is missing property [{propertyName}].");
        }

        PropertyInfo? key = typeof(ObjectAttributeUserTextRecipeRequest).GetProperty("Key");
        PropertyInfo? valueTemplate = typeof(ObjectAttributeUserTextRecipeRequest).GetProperty("ValueTemplate");
        RequireBulkAttributeRecipe(key is not null, "ObjectAttributeUserTextRecipeRequest is missing Key.");
        RequireBulkAttributeRecipe(valueTemplate is not null, "ObjectAttributeUserTextRecipeRequest is missing ValueTemplate.");
        DescriptionAttribute? valueTemplateDescription = valueTemplate!.GetCustomAttribute<DescriptionAttribute>();
        RequireBulkAttributeRecipe(
            valueTemplateDescription?.Description.Contains("{layerFullPath}", StringComparison.OrdinalIgnoreCase) == true,
            "ValueTemplate description should document supported tokens.");
    }

    private static void VerifySourceContracts()
    {
        string root = FindBulkAttributeRecipeRepositoryRoot();
        string servicePath = Path.Combine(root, "src", "MCP_Rhino.Server", "Application", "Services", "RhinoObjectAttributeRecipeService.cs");
        string skillPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Skills", "Editing", "ObjectAttributeRecipeSkill.cs");
        string cliPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "CLI", "DeveloperCommandHandler.cs");

        RequireBulkAttributeRecipe(File.Exists(servicePath), $"Recipe service source was not found: {servicePath}");
        RequireBulkAttributeRecipe(File.Exists(skillPath), $"Recipe skill source was not found: {skillPath}");
        RequireBulkAttributeRecipe(File.Exists(cliPath), $"DeveloperCommandHandler source was not found: {cliPath}");

        string serviceSource = File.ReadAllText(servicePath);
        string skillSource = File.ReadAllText(skillPath);
        string cliSource = File.ReadAllText(cliPath);

        foreach (string requiredFragment in new[]
        {
            "ExecuteWithUndo(request.FilePath, \"MCP: ApplyBulkObjectAttributeRecipe\"",
            "\"objectid\"",
            "user:[^}]",
            "ObjectNameTemplate",
            "RemoveUserTextKeys",
            "TargetLayerFullPath"
        })
        {
            RequireBulkAttributeRecipe(
                serviceSource.Contains(requiredFragment, StringComparison.Ordinal),
                $"Recipe service source should contain [{requiredFragment}].");
        }

        RequireBulkAttributeRecipe(
            !serviceSource.Contains("Process.Start", StringComparison.Ordinal)
                && !serviceSource.Contains(".py", StringComparison.OrdinalIgnoreCase),
            "Recipe service must not add arbitrary external script execution.");

        RequireBulkAttributeRecipe(
            skillSource.Contains("LiveObjectSelectionSkill", StringComparison.Ordinal)
                && skillSource.Contains("CreateSelectionRequest", StringComparison.Ordinal),
            "Recipe skill should compose live object selection before service execution.");

        RequireBulkAttributeRecipe(
            cliSource.Contains("partial void RegisterBulkAttributeRecipesHandlers();", StringComparison.Ordinal)
                && cliSource.Contains("RegisterBulkAttributeRecipesHandlers();", StringComparison.Ordinal),
            "DeveloperCommandHandler should register the bulk attribute recipe smoke hook.");
    }

    private static string FindBulkAttributeRecipeRepositoryRoot()
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

        directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MCP_Rhino.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static void RequireBulkAttributeRecipe(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
