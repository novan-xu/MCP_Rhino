using System.Reflection;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.Reference;
using MCP_Rhino.Server.Resources;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string RhinoReferenceResourcesSlug = "rhino-reference-resources-smoke-test";

    partial void RegisterRhinoReferenceResourcesHandlers()
    {
        _extensionHandlers[RhinoReferenceResourcesSlug] = HandleRhinoReferenceResourcesSmokeTest;
    }

    private bool HandleRhinoReferenceResourcesSmokeTest(string[] args)
    {
        try
        {
            RunRhinoReferenceResourcesSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Rhino reference resources smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunRhinoReferenceResourcesSmoke()
    {
        RhinoReferenceModuleListResponse modules = RhinoReferenceIndex.ListModules();
        RequireRhinoReference(modules.ModuleCount >= 3, "Expected at least three curated Rhino reference modules.");
        RequireRhinoReference(
            modules.Modules.Any(module => module.Name.Contains("geometry", StringComparison.OrdinalIgnoreCase)),
            "Expected geometry-related reference module.");

        RhinoReferenceSearchResponse search = RhinoReferenceIndex.Search("pipe", limit: 5);
        RequireRhinoReference(search.ResultCount > 0, "Expected pipe search to return at least one reference result.");
        RequireRhinoReference(
            search.Results.Any(result => string.Equals(result.FunctionName, "Brep.CreatePipe", StringComparison.OrdinalIgnoreCase)),
            "Expected pipe search to include Brep.CreatePipe.");

        RhinoReferenceFunctionDetailResponse? function = RhinoReferenceIndex.GetFunction("Brep.CreatePipe");
        RequireRhinoReference(function is not null, "Expected Brep.CreatePipe function detail.");
        RequireRhinoReference(
            function!.Notes.Any(note => note.Contains("CreatePipes", StringComparison.OrdinalIgnoreCase)),
            "Expected Brep.CreatePipe notes to mention CreatePipes.");

        RhinoReferenceFunctionDetailResponse? missing = RhinoReferenceIndex.GetFunction("ExecuteArbitraryRhinoScript");
        RequireRhinoReference(missing is null, "Arbitrary script execution must not be present in the reference index.");

        string modulesResource = RhinoReferenceResource.ListRhinoReferenceModulesResource();
        RequireRhinoReference(
            modulesResource.Contains("Rhino Reference Modules", StringComparison.Ordinal)
            && modulesResource.Contains(RhinoReferenceIndex.Source, StringComparison.Ordinal),
            "Module resource markdown should include title and source.");

        string missingResource = RhinoReferenceResource.GetRhinoReferenceFunctionResource("ExecuteArbitraryRhinoScript");
        RequireRhinoReference(
            missingResource.Contains("Function Not Found", StringComparison.Ordinal),
            "Missing function resource should return bounded not-found markdown.");

        McpRhinoToolFamilyListResponse families = McpRhinoToolHelpIndex.ListFamilies();
        RequireRhinoReference(families.FamilyCount > 0, "Expected MCP tool family help.");
        RequireRhinoReference(
            families.Families.Any(family => string.Equals(family.Family, "Reference", StringComparison.Ordinal)),
            "Expected Reference tool family in generated tool help.");

        McpRhinoToolHelpResponse toolHelp = McpRhinoToolHelpIndex.Search("CreatePipes", limit: 5);
        RequireRhinoReference(
            toolHelp.Tools.Any(tool => string.Equals(tool.ToolName, "CreatePipes", StringComparison.Ordinal)),
            "Expected generated tool help to include CreatePipes.");

        string toolResource = McpRhinoToolHelpResource.GetMcpRhinoToolHelpResource("CreatePipes");
        RequireRhinoReference(
            toolResource.Contains("CreatePipes", StringComparison.Ordinal)
            && toolResource.Contains("Safety:", StringComparison.Ordinal),
            "Tool help resource should include tool name and safety metadata.");

        List<string> resourceMembers = GetRhinoReferenceResourceMembers();
        RequireRhinoReference(resourceMembers.Count >= 5, $"Expected at least 5 MCP resource members, found {resourceMembers.Count}.");

        Console.WriteLine("[OK] rhino-reference-resources smoke verified curated modules, function lookup, missing-function handling, generated tool help, and MCP resource members.");
        Console.WriteLine($"[OK] Reference modules={modules.ModuleCount}; resourceMembers={resourceMembers.Count}; toolFamilies={families.FamilyCount}.");
    }

    private static List<string> GetRhinoReferenceResourceMembers()
    {
        return GetLoadableResourceTypes(typeof(DeveloperCommandHandler).Assembly)
            .Where(type => type.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .SelectMany(type => type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(member => member.GetCustomAttribute<McpServerResourceAttribute>() is not null)
                .Select(member => $"{type.Name}.{member.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<Type> GetLoadableResourceTypes(Assembly assembly)
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

    private static void RequireRhinoReference(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
