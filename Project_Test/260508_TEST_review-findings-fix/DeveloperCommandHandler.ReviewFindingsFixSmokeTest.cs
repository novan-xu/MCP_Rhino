using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Tools.Analysis;
using MCP_Rhino.Server.Skills.Editing;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ReviewFindingsFixSlug = "review-findings-fix-smoke-test";

    partial void RegisterReviewFindingsFixHandlers()
    {
        _extensionHandlers[ReviewFindingsFixSlug] = HandleReviewFindingsFixSmokeTest;
    }

    private bool HandleReviewFindingsFixSmokeTest(string[] args)
    {
        try
        {
            RunReviewFindingsFixSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Review findings fix smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunReviewFindingsFixSmoke()
    {
        string root = FindReviewFindingsFixRepositoryRoot();

        RequireUndoRollbackGuard(root);
        RequireFilterObjectsAmbiguitySafeRouting(root);
        RequireCleanMcpToolDescriptions();
        RequireRuntimeWorkflowUsesCanonicalLayerTool(root);

        Console.WriteLine("[OK] Review findings fix smoke verified undo rollback guard, FilterObjects routing, MCP descriptions, and workflow tool names.");
    }

    private static void RequireUndoRollbackGuard(string root)
    {
        string accessorPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveRhinoDocumentAccessorBase.cs");
        string source = File.ReadAllText(accessorPath);

        RequireReviewFix(source.Contains("uint currentUndoRecordBefore = document.CurrentUndoRecordSerialNumber;", StringComparison.Ordinal),
            "ExecuteWithUndo must capture the current undo record before mutation.");
        RequireReviewFix(source.Contains("RollBackFailedUndoRecord(document, currentUndoRecordBefore)", StringComparison.Ordinal),
            "ExecuteWithUndo must attempt rollback when work fails.");
        RequireReviewFix(source.Contains("document.Undo()", StringComparison.Ordinal),
            "Rollback path must call RhinoDoc.Undo().");
        RequireReviewFix(source.Contains("document.CurrentUndoRecordSerialNumber == currentUndoRecordBefore", StringComparison.Ordinal),
            "Rollback path must guard against undoing unrelated previous user work.");
    }

    private static void RequireFilterObjectsAmbiguitySafeRouting(string root)
    {
        ConstructorInfo[] constructors = typeof(FilterObjectsTool).GetConstructors();
        RequireReviewFix(constructors.Length == 1, "FilterObjectsTool should have one public constructor.");
        ParameterInfo[] parameters = constructors[0].GetParameters();
        RequireReviewFix(parameters.Length == 1 && parameters[0].ParameterType == typeof(LiveObjectSelectionSkill),
            "FilterObjectsTool must use LiveObjectSelectionSkill so layer queries go through ambiguity resolution.");

        MethodInfo method = typeof(FilterObjectsTool).GetMethod("FilterObjects")
            ?? throw new MissingMethodException(nameof(FilterObjectsTool), "FilterObjects");
        string description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        RequireReviewFix(description.Contains("ambiguous layer queries fail", StringComparison.OrdinalIgnoreCase),
            "FilterObjects description must state ambiguous layer-query behavior.");

        string filterToolPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Tools", "Analysis", "FilterObjectsTool.cs");
        string source = File.ReadAllText(filterToolPath);
        RequireReviewFix(source.Contains("_objectSelectionSkill.Select", StringComparison.Ordinal),
            "FilterObjectsTool source must delegate to LiveObjectSelectionSkill.Select.");
        RequireReviewFix(!source.Contains("_filterService.Filter", StringComparison.Ordinal),
            "FilterObjectsTool must not bypass ambiguity resolution by calling RhinoObjectFilterService.Filter directly.");
    }

    private static void RequireCleanMcpToolDescriptions()
    {
        string[] forbiddenFragments =
        {
            "Rhino .3dm file",
            ".3dm file",
            "overwrite original file",
            "覆盖写回",
            "直接覆盖",
            "不落盘",
            "閿",
            "鐢",
            "妫",
            "璇",
            "俓"
        };

        foreach (Type type in GetReviewFixLoadableTypes(typeof(DeveloperCommandHandler).Assembly)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null))
            {
                string description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
                foreach (string fragment in forbiddenFragments)
                {
                    RequireReviewFix(!description.Contains(fragment, StringComparison.Ordinal),
                        $"MCP tool description contains stale or malformed routing text: {type.Name}.{method.Name} -> {fragment}");
                }
            }
        }
    }

    private static void RequireRuntimeWorkflowUsesCanonicalLayerTool(string root)
    {
        string workflowPath = Path.Combine(root, "Runtime_Workflow", "MCP_Rhino Workflow.md");
        string source = File.ReadAllText(workflowPath);
        RequireReviewFix(!source.Contains("GetLayersInLiveTool", StringComparison.Ordinal),
            "Runtime workflow must not reference removed GetLayersInLiveTool.");
        RequireReviewFix(source.Contains("\"GetLayers\"", StringComparison.Ordinal),
            "Runtime workflow example should reference canonical GetLayers.");
    }

    private static IEnumerable<Type> GetReviewFixLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }

    private static string FindReviewFindingsFixRepositoryRoot()
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

    private static void RequireReviewFix(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
