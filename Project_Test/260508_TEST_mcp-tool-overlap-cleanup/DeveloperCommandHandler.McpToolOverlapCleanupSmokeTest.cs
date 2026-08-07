using System.Reflection;
using MCP_Rhino.Server.Contracts.Responses;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string McpToolOverlapCleanupSlug = "mcp-tool-overlap-cleanup-smoke-test";

    partial void RegisterMcpToolOverlapCleanupHandlers()
    {
        _extensionHandlers[McpToolOverlapCleanupSlug] = HandleMcpToolOverlapCleanupSmokeTest;
    }

    private bool HandleMcpToolOverlapCleanupSmokeTest(string[] args)
    {
        try
        {
            RunMcpToolOverlapCleanupSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP tool overlap cleanup smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunMcpToolOverlapCleanupSmoke()
    {
        List<ToolInventoryItem> tools = GetToolInventory();
        Dictionary<string, ToolInventoryItem> byName = tools.ToDictionary(item => item.MethodName, StringComparer.Ordinal);

        RequireOverlapCleanup(tools.Count == 169, $"Expected 169 MCP tools after Grasshopper authoring additions, found {tools.Count}.");

        RequirePresent(byName, "FilterObjects");
        RequirePresent(byName, "GetObjectMetricsByFilter");
        RequirePresent(byName, "GetMassPropertiesByFilter");
        RequirePresent(byName, "GetGeometryFramesByFilter");
        RequirePresent(byName, "GetCurvatureSamplesByFilter");
        RequirePresent(byName, "GetContourCurvesByFilter");
        RequirePresent(byName, "FindLayerCandidates");
        RequirePresent(byName, "GetLayers");
        RequirePresent(byName, "GetObjectUserStrings");
        RequirePresent(byName, "PreviewBulkObjectAttributeRecipe");
        RequirePresent(byName, "ApplyBulkObjectAttributeRecipe");
        RequirePresent(byName, "PreviewObjectUserTextWrites");
        RequirePresent(byName, "GetDocumentUserStrings");
        RequirePresent(byName, "PreviewCreateBlockDefinitions");
        RequirePresent(byName, "ApplyCreateBlockDefinitions");
        RequirePresent(byName, "PreviewInsertBlockInstances");
        RequirePresent(byName, "ApplyInsertBlockInstances");
        RequirePresent(byName, "InspectTakeoffSources");
        RequirePresent(byName, "PreviewTakeoffSchedule");
        RequirePresent(byName, "ExportTakeoffSchedule");
        RequirePresent(byName, "RunTakeoffSpreadsheetAgent");

        RequireAbsent(byName,
            "FilterObjectsInLive",
            "FilterObjectsByType",
            "FilterObjectsByUserAttributes",
            "FilterObjectsByLayer",
            "GetLayersInLive",
            "FindLayerCandidatesInLive",
            "GetObjectUserStringsInLive",
            "PreviewObjectUserTextWritesInLive",
            "GetDocumentUserStringsInLive",
            "CreateBlockDefinitions",
            "InsertBlockInstances");

        RequireFamilyCount(tools, "Analysis", 20);
        RequireFamilyCount(tools, "Layers", 11);
        RequireFamilyCount(tools, "Editing", 8);
        RequireFamilyCount(tools, "File", 3);
        RequireFamilyCount(tools, "Geometry/Architecture", 11);
        RequireFamilyCount(tools, "Blocks", 14);

        MethodInfo filterObjects = byName["FilterObjects"].Method;
        RequireOverlapCleanup(
            filterObjects.ReturnType.IsGenericType
                && filterObjects.ReturnType.GetGenericTypeDefinition() == typeof(OperationResponse<>)
                && filterObjects.ReturnType.GenericTypeArguments[0].Name == "RhinoObjectFilterResult",
            "FilterObjects returns structured live RhinoObjectFilterResult.");

        MethodInfo findLayerCandidates = byName["FindLayerCandidates"].Method;
        RequireOverlapCleanup(
            findLayerCandidates.ReturnType.IsGenericType
                && findLayerCandidates.ReturnType.GetGenericTypeDefinition() == typeof(OperationResponse<>)
                && findLayerCandidates.ReturnType.GenericTypeArguments[0].Name.Contains("IReadOnlyList", StringComparison.Ordinal),
            "FindLayerCandidates returns a structured OperationResponse list.");

        Console.WriteLine("[OK] MCP tool overlap cleanup smoke verified canonical tools and removed duplicate wrappers.");
        Console.WriteLine("[OK] MCP tool count=169; removed duplicate wrappers=11.");
    }

    private static List<ToolInventoryItem> GetToolInventory()
    {
        return GetLoadableTypesForOverlapCleanup(typeof(DeveloperCommandHandler).Assembly)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Select(method => new ToolInventoryItem(GetToolFamily(type), type.Name, method.Name, method)))
            .ToList();
    }

    private static IEnumerable<Type> GetLoadableTypesForOverlapCleanup(Assembly assembly)
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

    private static string GetToolFamily(Type type)
    {
        const string marker = ".Tools.";
        string? namespaceName = type.Namespace;
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return "(no namespace)";
        }

        int markerIndex = namespaceName.IndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0
            ? namespaceName
            : namespaceName[(markerIndex + marker.Length)..].Replace('.', '/');
    }

    private static void RequirePresent(IReadOnlyDictionary<string, ToolInventoryItem> tools, string methodName)
    {
        RequireOverlapCleanup(tools.ContainsKey(methodName), $"Expected canonical MCP tool was not found: {methodName}");
    }

    private static void RequireAbsent(IReadOnlyDictionary<string, ToolInventoryItem> tools, params string[] methodNames)
    {
        foreach (string methodName in methodNames)
        {
            RequireOverlapCleanup(!tools.ContainsKey(methodName), $"Duplicate MCP tool should have been removed: {methodName}");
        }
    }

    private static void RequireFamilyCount(IReadOnlyList<ToolInventoryItem> tools, string family, int expectedCount)
    {
        int actual = tools.Count(item => string.Equals(item.Family, family, StringComparison.Ordinal));
        RequireOverlapCleanup(actual == expectedCount, $"Expected {expectedCount} tools in family {family}, found {actual}.");
    }

    private static void RequireOverlapCleanup(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct ToolInventoryItem(string Family, string ClassName, string MethodName, MethodInfo Method);
}
