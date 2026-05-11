using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Requests;
using MCP_Rhino.Server.Skills.Inspection;
using MCP_Rhino.Server.Tools.Analysis;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string SelectionScopedAnalysisRecipesSlug = "selection-scoped-analysis-recipes-smoke-test";

    partial void RegisterSelectionScopedAnalysisRecipeHandlers()
    {
        _extensionHandlers[SelectionScopedAnalysisRecipesSlug] = HandleSelectionScopedAnalysisRecipesSmokeTest;
    }

    private bool HandleSelectionScopedAnalysisRecipesSmokeTest(string[] args)
    {
        try
        {
            RunSelectionScopedAnalysisRecipesSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Selection-scoped analysis recipes smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunSelectionScopedAnalysisRecipesSmoke()
    {
        VerifySelectionScopedAnalysisTool(
            typeof(GetObjectMetricsByFilterTool),
            "GetObjectMetricsByFilter",
            new[] { "server-side", "avoid sending large ObjectId payloads", "GetObjectMetricsInLive" },
            CommonSelectionParameterNames());

        VerifySelectionScopedAnalysisTool(
            typeof(GetMassPropertiesByFilterTool),
            "GetMassPropertiesByFilter",
            new[] { "server-side", "avoid sending large ObjectId payloads", "Length, Area, Volume, and Auto" },
            CommonSelectionParameterNames().Concat(new[] { "kind" }).ToArray());

        VerifySelectionScopedAnalysisTool(
            typeof(GetGeometryFramesByFilterTool),
            "GetGeometryFramesByFilter",
            new[] { "server-side", "one common frame recipe", "avoid large per-object entry payloads" },
            CommonSelectionParameterNames().Concat(new[] { "kind", "parameter", "u", "v", "parameterSpec", "entryIdPrefix" }).ToArray());

        VerifySelectionScopedAnalysisTool(
            typeof(GetCurvatureSamplesByFilterTool),
            "GetCurvatureSamplesByFilter",
            new[] { "server-side", "one common curvature recipe", "avoid large per-object entry payloads" },
            CommonSelectionParameterNames().Concat(new[] { "mode", "sampleCount", "stepLength", "parameters", "uvSamples", "entryIdPrefix" }).ToArray());

        VerifySelectionScopedAnalysisTool(
            typeof(GetContourCurvesByFilterTool),
            "GetContourCurvesByFilter",
            new[] { "server-side", "one common contour recipe", "avoid large per-object entry payloads" },
            CommonSelectionParameterNames().Concat(new[] { "startX", "startY", "startZ", "endX", "endY", "endZ", "interval", "entryIdPrefix" }).ToArray());

        VerifySelectionScopedAnalysisRequestContracts();
        VerifySelectionScopedAnalysisSourceContracts();

        Console.WriteLine("[OK] Selection-scoped analysis recipe tools expose five read-only filter-resolved analysis methods.");
        Console.WriteLine("[OK] Selection-scoped analysis recipes reuse live object selection and existing analysis services.");
    }

    private static string[] CommonSelectionParameterNames()
    {
        return new[]
        {
            "filePath",
            "layerQueries",
            "confirmedLayerFullPaths",
            "objectTypes",
            "userAttributeConditions",
            "matchMode",
            "userAttributeMatchMode"
        };
    }

    private static void VerifySelectionScopedAnalysisTool(
        Type toolType,
        string methodName,
        IReadOnlyList<string> expectedDescriptionFragments,
        IReadOnlyList<string> expectedParameterNames)
    {
        RequireSelectionScopedAnalysis(
            toolType.GetCustomAttribute<McpServerToolTypeAttribute>() is not null,
            $"{toolType.Name} is missing [McpServerToolType].");

        MethodInfo? method = toolType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        RequireSelectionScopedAnalysis(method is not null, $"Tool method was not found: {toolType.Name}.{methodName}");
        MethodInfo methodInfo = method!;

        McpServerToolAttribute? tool = methodInfo.GetCustomAttribute<McpServerToolAttribute>();
        RequireSelectionScopedAnalysis(tool is not null, $"{methodName} is missing [McpServerTool].");
        RequireSelectionScopedAnalysis(tool!.ReadOnly == true, $"{methodName} should be read-only.");
        RequireSelectionScopedAnalysis(tool.Destructive == false, $"{methodName} should not be destructive.");
        RequireSelectionScopedAnalysis(tool.OpenWorld == false, $"{methodName} should be closed-world.");

        string description = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        foreach (string fragment in expectedDescriptionFragments)
        {
            RequireSelectionScopedAnalysis(
                description.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"{methodName} description should include [{fragment}].");
        }

        HashSet<string> parameterNames = methodInfo.GetParameters()
            .Select(parameter => parameter.Name ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string parameterName in expectedParameterNames)
        {
            RequireSelectionScopedAnalysis(parameterNames.Contains(parameterName), $"{methodName} is missing parameter [{parameterName}].");
        }
    }

    private static void VerifySelectionScopedAnalysisRequestContracts()
    {
        foreach (Type requestType in new[]
        {
            typeof(GetObjectMetricsByFilterRequest),
            typeof(GetMassPropertiesByFilterRequest),
            typeof(GetGeometryFramesByFilterRequest),
            typeof(GetCurvatureSamplesByFilterRequest),
            typeof(GetContourCurvesByFilterRequest)
        })
        {
            RequireSelectionScopedAnalysis(
                requestType.BaseType == typeof(SelectionScopedAnalysisRequestBase),
                $"{requestType.Name} should inherit SelectionScopedAnalysisRequestBase.");
        }

        foreach (string propertyName in new[]
        {
            "FilePath",
            "LayerQueries",
            "ConfirmedLayerFullPaths",
            "ObjectTypes",
            "UserAttributeConditions",
            "MatchMode",
            "UserAttributeMatchMode"
        })
        {
            RequireSelectionScopedAnalysis(
                typeof(SelectionScopedAnalysisRequestBase).GetProperty(propertyName) is not null,
                $"SelectionScopedAnalysisRequestBase is missing property [{propertyName}].");
        }

        RequireSelectionScopedAnalysis(typeof(GetMassPropertiesByFilterRequest).GetProperty("Kind") is not null, "Mass-properties recipe should expose Kind.");
        RequireSelectionScopedAnalysis(typeof(GetGeometryFramesByFilterRequest).GetProperty("ParameterSpec") is not null, "Frame recipe should expose ParameterSpec.");
        RequireSelectionScopedAnalysis(typeof(GetCurvatureSamplesByFilterRequest).GetProperty("UvSamples") is not null, "Curvature recipe should expose UvSamples.");
        RequireSelectionScopedAnalysis(typeof(GetContourCurvesByFilterRequest).GetProperty("Interval") is not null, "Contour recipe should expose Interval.");
    }

    private static void VerifySelectionScopedAnalysisSourceContracts()
    {
        string root = FindSelectionScopedAnalysisRepositoryRoot();
        string skillPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Skills", "Inspection", "SelectionScopedAnalysisSkill.cs");
        string agentRegistrationPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Server", "AgentRegistration.cs");
        string cliPath = Path.Combine(root, "src", "MCP_Rhino.Server", "Infrastructure", "CLI", "DeveloperCommandHandler.cs");

        RequireSelectionScopedAnalysis(File.Exists(skillPath), $"Selection-scoped analysis skill source was not found: {skillPath}");
        RequireSelectionScopedAnalysis(File.Exists(agentRegistrationPath), $"AgentRegistration source was not found: {agentRegistrationPath}");
        RequireSelectionScopedAnalysis(File.Exists(cliPath), $"DeveloperCommandHandler source was not found: {cliPath}");

        string skillSource = File.ReadAllText(skillPath);
        string agentRegistrationSource = File.ReadAllText(agentRegistrationPath);
        string cliSource = File.ReadAllText(cliPath);

        foreach (string requiredFragment in new[]
        {
            "LiveObjectSelectionSkill",
            "GetObjectMetricsInLive",
            "GetMassPropertiesInLive",
            "GetGeometryFramesInLive",
            "GetCurvatureSamplesInLive",
            "GetContourCurvesInLive",
            "MaxObjectIdsPerRequest"
        })
        {
            RequireSelectionScopedAnalysis(
                skillSource.Contains(requiredFragment, StringComparison.Ordinal),
                $"Selection-scoped analysis skill should contain [{requiredFragment}].");
        }

        RequireSelectionScopedAnalysis(
            !skillSource.Contains("Process.Start", StringComparison.Ordinal)
                && !skillSource.Contains(".py", StringComparison.OrdinalIgnoreCase),
            "Selection-scoped analysis recipes must not add arbitrary external script execution.");

        RequireSelectionScopedAnalysis(
            agentRegistrationSource.Contains("SelectionScopedAnalysisSkill", StringComparison.Ordinal),
            "AgentRegistration should register SelectionScopedAnalysisSkill.");

        RequireSelectionScopedAnalysis(
            cliSource.Contains("partial void RegisterSelectionScopedAnalysisRecipeHandlers();", StringComparison.Ordinal)
                && cliSource.Contains("RegisterSelectionScopedAnalysisRecipeHandlers();", StringComparison.Ordinal),
            "DeveloperCommandHandler should register the selection-scoped analysis recipes smoke hook.");
    }

    private static string FindSelectionScopedAnalysisRepositoryRoot()
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

    private static void RequireSelectionScopedAnalysis(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
