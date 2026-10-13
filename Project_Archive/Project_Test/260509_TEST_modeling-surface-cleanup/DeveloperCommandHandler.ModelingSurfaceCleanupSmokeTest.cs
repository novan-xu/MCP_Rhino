using System.Reflection;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string ModelingSurfaceCleanupSlug = "modeling-surface-cleanup-smoke-test";

    partial void RegisterModelingSurfaceCleanupHandlers()
    {
        _extensionHandlers[ModelingSurfaceCleanupSlug] = HandleModelingSurfaceCleanupSmokeTest;
    }

    private bool HandleModelingSurfaceCleanupSmokeTest(string[] args)
    {
        try
        {
            RunModelingSurfaceCleanupSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Modeling surface cleanup smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunModelingSurfaceCleanupSmoke()
    {
        Assembly assembly = typeof(DeveloperCommandHandler).Assembly;
        List<Type> loadableTypes = GetModelingSurfaceCleanupLoadableTypes(assembly).ToList();
        var typeNames = loadableTypes
            .Select(type => type.FullName ?? type.Name)
            .ToHashSet(StringComparer.Ordinal);

        RequireModelingSurfaceCleanup(
            !typeNames.Contains("MCP_Rhino.Server.Skills.Modeling.BuildingMassingSkill"),
            "BuildingMassingSkill should not be present.");
        RequireModelingSurfaceCleanup(
            !typeNames.Contains("MCP_Rhino.Server.Tools.Geometry.Architecture.CreateBuildingMassingTool"),
            "CreateBuildingMassingTool should not be present.");
        RequireModelingSurfaceCleanup(
            !typeNames.Contains("MCP_Rhino.Server.Contracts.Requests.CreateBuildingMassingRequest"),
            "CreateBuildingMassingRequest should not be present.");
        RequireModelingSurfaceCleanup(
            !typeNames.Contains("MCP_Rhino.Server.Contracts.Responses.BuildingMassingResponse"),
            "BuildingMassingResponse should not be present.");
        RequireModelingSurfaceCleanup(
            !typeNames.Contains("MCP_Rhino.Server.Domain.Models.BuildingMassingSpec"),
            "BuildingMassingSpec should not be present.");

        RequireModelingSurfaceCleanup(
            typeNames.Contains("MCP_Rhino.Server.Agents.Modeling.ReferenceImageObjectModelingAgent"),
            "ReferenceImageObjectModelingAgent should remain present.");

        var toolNames = loadableTypes
            .Where(type => (type.FullName ?? string.Empty).Contains(".Tools.", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        RequireModelingSurfaceCleanup(!toolNames.Contains("CreateBuildingMassing"), "CreateBuildingMassing should not be exposed as an MCP tool.");

        foreach (string retainedTool in new[]
        {
            "CreateBoxes",
            "CreateExtrusions",
            "CreatePlanarBreps",
            "CreateSlabs",
            "CreateWalls",
            "CreateColumns",
            "CreateBeams",
            "PreviewBooleanObjects",
            "ApplyBooleanObjects",
            "PreviewOpenings",
            "ApplyOpenings"
        })
        {
            RequireModelingSurfaceCleanup(toolNames.Contains(retainedTool), $"Expected retained atomic tool was not found: {retainedTool}");
        }

        Console.WriteLine("[OK] modeling surface cleanup removed CreateBuildingMassing and preserved atomic primitive/boolean tools.");
    }

    private static IEnumerable<Type> GetModelingSurfaceCleanupLoadableTypes(Assembly assembly)
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

    private static void RequireModelingSurfaceCleanup(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
