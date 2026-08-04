using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string McpSurfaceStructureGovernanceSlug = "mcp-surface-structure-governance-smoke-test";

    partial void RegisterMcpSurfaceStructureGovernanceHandlers()
    {
        _extensionHandlers[McpSurfaceStructureGovernanceSlug] = HandleMcpSurfaceStructureGovernanceSmokeTest;
    }

    private bool HandleMcpSurfaceStructureGovernanceSmokeTest(string[] args)
    {
        try
        {
            RunMcpSurfaceStructureGovernanceSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MCP surface structure governance smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunMcpSurfaceStructureGovernanceSmoke()
    {
        Assembly assembly = typeof(DeveloperCommandHandler).Assembly;
        List<McpSurfaceToolInventoryItem> toolItems = GetMcpSurfaceToolInventory(assembly);

        RequireMcpSurface(toolItems.Count > 0, "No MCP tools were discovered.");
        RequireUniqueMcpToolNames(toolItems);
        RequireMcpToolDescriptions(toolItems);
        RequireExplicitMcpToolSafetyMetadata(toolItems.Count);

        List<McpSurfaceResourceInventoryItem> resourceItems = GetMcpSurfaceResourceInventory(assembly);

        Console.WriteLine($"[OK] MCP tool inventory discovered {toolItems.Count} tools.");
        foreach (var familyGroup in toolItems.GroupBy(item => item.Family).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"[INFO] Tool family {familyGroup.Key}: {familyGroup.Count()}");
        }

        foreach (McpSurfaceToolInventoryItem item in toolItems.OrderBy(item => item.Family, StringComparer.Ordinal).ThenBy(item => item.MethodName, StringComparer.Ordinal))
        {
            Console.WriteLine($"[TOOL] {item.Family} :: {item.ClassName}.{item.MethodName} :: ReadOnly={item.ReadOnly}, Destructive={item.Destructive}, OpenWorld={item.OpenWorld}");
        }

        Console.WriteLine($"[OK] MCP resource inventory discovered {resourceItems.Count} resources.");
        foreach (McpSurfaceResourceInventoryItem item in resourceItems.OrderBy(item => item.ClassName, StringComparer.Ordinal).ThenBy(item => item.MemberName, StringComparer.Ordinal))
        {
            Console.WriteLine($"[RESOURCE] {item.ClassName}.{item.MemberName}");
        }
    }

    private static List<McpSurfaceToolInventoryItem> GetMcpSurfaceToolInventory(Assembly assembly)
    {
        return GetMcpSurfaceLoadableTypes(assembly)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => new
                {
                    Type = type,
                    Method = method,
                    Tool = method.GetCustomAttribute<McpServerToolAttribute>(),
                    Description = method.GetCustomAttribute<DescriptionAttribute>()
                }))
            .Where(item => item.Tool is not null)
            .Select(item => new McpSurfaceToolInventoryItem(
                Family: GetMcpSurfaceToolFamily(item.Type),
                ClassName: item.Type.Name,
                MethodName: item.Method.Name,
                ReadOnly: item.Tool!.ReadOnly,
                Destructive: item.Tool.Destructive,
                OpenWorld: item.Tool.OpenWorld,
                Description: item.Description?.Description ?? string.Empty))
            .ToList();
    }

    private static List<McpSurfaceResourceInventoryItem> GetMcpSurfaceResourceInventory(Assembly assembly)
    {
        return GetMcpSurfaceLoadableTypes(assembly)
            .Where(type => type.GetCustomAttribute<McpServerResourceTypeAttribute>() is not null)
            .SelectMany(type => type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(member => member.GetCustomAttribute<McpServerResourceAttribute>() is not null)
                .Select(member => new McpSurfaceResourceInventoryItem(type.Name, member.Name)))
            .ToList();
    }

    private static IEnumerable<Type> GetMcpSurfaceLoadableTypes(Assembly assembly)
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

    private static string GetMcpSurfaceToolFamily(Type type)
    {
        const string marker = ".Tools.";
        string? namespaceName = type.Namespace;
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return "(no namespace)";
        }

        int markerIndex = namespaceName.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return namespaceName;
        }

        return namespaceName[(markerIndex + marker.Length)..].Replace('.', '/');
    }

    private static void RequireUniqueMcpToolNames(List<McpSurfaceToolInventoryItem> toolItems)
    {
        List<string> duplicates = toolItems
            .GroupBy(item => item.MethodName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(item => item.ClassName).Order(StringComparer.Ordinal))})")
            .ToList();

        RequireMcpSurface(duplicates.Count == 0, "Duplicate MCP tool method names: " + string.Join("; ", duplicates));
    }

    private static void RequireMcpToolDescriptions(List<McpSurfaceToolInventoryItem> toolItems)
    {
        List<string> missingDescriptions = toolItems
            .Where(item => string.IsNullOrWhiteSpace(item.Description))
            .Select(item => $"{item.ClassName}.{item.MethodName}")
            .Order(StringComparer.Ordinal)
            .ToList();

        RequireMcpSurface(missingDescriptions.Count == 0, "MCP tools missing method-level Description: " + string.Join(", ", missingDescriptions));
    }

    private static void RequireExplicitMcpToolSafetyMetadata(int reflectedToolCount)
    {
        string root = FindMcpSurfaceRepositoryRoot();
        string toolsRoot = Path.Combine(root, "src", "MCP_Rhino.Server", "Tools");
        RequireMcpSurface(Directory.Exists(toolsRoot), $"Tools source directory was not found: {toolsRoot}");

        var attributePattern = new Regex(@"\[McpServerTool(?:\((?<args>.*?)\))?\]", RegexOptions.Compiled | RegexOptions.Singleline);
        int sourceAttributeCount = 0;

        foreach (string sourcePath in Directory.EnumerateFiles(toolsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (Match match in attributePattern.Matches(source))
            {
                sourceAttributeCount++;
                string relativePath = Path.GetRelativePath(root, sourcePath);
                string args = match.Groups["args"].Value;

                RequireMcpSurface(match.Groups["args"].Success, $"Bare [McpServerTool] attribute found: {relativePath}");
                RequireMcpSurface(args.Contains("ReadOnly =", StringComparison.Ordinal), $"Missing ReadOnly metadata: {relativePath}");
                RequireMcpSurface(args.Contains("Destructive =", StringComparison.Ordinal), $"Missing Destructive metadata: {relativePath}");
                RequireMcpSurface(args.Contains("OpenWorld =", StringComparison.Ordinal), $"Missing OpenWorld metadata: {relativePath}");
            }
        }

        RequireMcpSurface(sourceAttributeCount == reflectedToolCount,
            $"Reflected MCP tool count must match source annotation count. Reflected={reflectedToolCount}, Source={sourceAttributeCount}.");
    }

    private static string FindMcpSurfaceRepositoryRoot()
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

    private static void RequireMcpSurface(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private readonly record struct McpSurfaceToolInventoryItem(
        string Family,
        string ClassName,
        string MethodName,
        bool ReadOnly,
        bool Destructive,
        bool OpenWorld,
        string Description);

    private readonly record struct McpSurfaceResourceInventoryItem(string ClassName, string MemberName);
}
