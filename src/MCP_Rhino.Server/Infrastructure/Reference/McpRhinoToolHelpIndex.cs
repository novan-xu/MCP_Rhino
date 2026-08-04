using System.ComponentModel;
using System.Reflection;
using MCP_Rhino.Server.Contracts.Responses;
using MCP_Rhino.Server.Infrastructure.CLI;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.Reference;

public static class McpRhinoToolHelpIndex
{
    public static McpRhinoToolFamilyListResponse ListFamilies()
    {
        List<McpRhinoToolHelpEntryResponse> tools = GetTools();
        List<McpRhinoToolFamilyResponse> families = tools
            .GroupBy(tool => tool.Family, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new McpRhinoToolFamilyResponse
            {
                Family = group.Key,
                ToolCount = group.Count(),
                ToolNames = group.Select(tool => tool.ToolName).Order(StringComparer.Ordinal).ToList()
            })
            .ToList();

        return new McpRhinoToolFamilyListResponse
        {
            FamilyCount = families.Count,
            Families = families
        };
    }

    public static McpRhinoToolHelpResponse Search(string? toolName = null, string? family = null, int limit = 20)
    {
        IEnumerable<McpRhinoToolHelpEntryResponse> tools = GetTools();
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            string normalized = toolName.Trim();
            tools = tools.Where(tool =>
                tool.ToolName.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || tool.Description.Contains(normalized, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(family))
        {
            string normalizedFamily = family.Trim();
            tools = tools.Where(tool => tool.Family.Contains(normalizedFamily, StringComparison.OrdinalIgnoreCase));
        }

        List<McpRhinoToolHelpEntryResponse> matches = tools
            .OrderBy(tool => tool.Family, StringComparer.Ordinal)
            .ThenBy(tool => tool.ToolName, StringComparer.Ordinal)
            .Take(Math.Clamp(limit <= 0 ? 20 : limit, 1, 100))
            .ToList();

        return new McpRhinoToolHelpResponse
        {
            ToolCount = matches.Count,
            Tools = matches
        };
    }

    public static string FormatFamiliesMarkdown()
    {
        McpRhinoToolFamilyListResponse families = ListFamilies();
        var lines = new List<string>
        {
            "# MCP_Rhino Tool Families",
            string.Empty
        };

        foreach (McpRhinoToolFamilyResponse family in families.Families)
        {
            lines.Add($"## {family.Family}");
            lines.Add($"Tools: {family.ToolCount}");
            lines.AddRange(family.ToolNames.Select(name => $"- {name}"));
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string FormatToolMarkdown(string toolName)
    {
        McpRhinoToolHelpEntryResponse? tool = Search(toolName, null, 1).Tools
            .FirstOrDefault(item => string.Equals(item.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
        {
            return $"# MCP_Rhino Tool Not Found{Environment.NewLine}{Environment.NewLine}No MCP tool is named `{toolName}`.";
        }

        var lines = new List<string>
        {
            $"# {tool.ToolName}",
            string.Empty,
            $"Family: `{tool.Family}`",
            $"Class: `{tool.ClassName}`",
            $"Safety: ReadOnly={tool.ReadOnly}, Destructive={tool.Destructive}, OpenWorld={tool.OpenWorld}",
            string.Empty,
            tool.Description,
            string.Empty,
            "Parameters:"
        };
        lines.AddRange(tool.Parameters.Select(parameter => $"- {parameter}"));
        return string.Join(Environment.NewLine, lines);
    }

    private static List<McpRhinoToolHelpEntryResponse> GetTools()
    {
        return GetLoadableTypes(typeof(DeveloperCommandHandler).Assembly)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
                .Select(method =>
                {
                    McpServerToolAttribute tool = method.GetCustomAttribute<McpServerToolAttribute>()!;
                    return new McpRhinoToolHelpEntryResponse
                    {
                        Family = GetToolFamily(type),
                        ToolName = method.Name,
                        ClassName = type.Name,
                        Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
                        ReadOnly = tool.ReadOnly,
                        Destructive = tool.Destructive,
                        OpenWorld = tool.OpenWorld,
                        Parameters = method.GetParameters()
                            .Select(parameter => $"{parameter.Name}: {FormatParameterType(parameter.ParameterType)}")
                            .ToList()
                    };
                }))
            .ToList();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
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

    private static string FormatParameterType(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string genericName = type.Name[..type.Name.IndexOf('`')];
        return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(FormatParameterType))}>";
    }
}
