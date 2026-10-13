using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace MCP_Rhino.Server.Infrastructure.CLI;

public sealed partial class DeveloperCommandHandler
{
    private const string RuntimeTextNormalizationSlug = "runtime-text-normalization-smoke-test";

    private static readonly Regex RuntimeTextCjkPattern = new(
        @"[\u3000-\u303F\u3400-\u9FFF\uF900-\uFAFF\uFF00-\uFFEF]",
        RegexOptions.Compiled);

    partial void RegisterRuntimeTextNormalizationHandlers()
    {
        _extensionHandlers[RuntimeTextNormalizationSlug] = HandleRuntimeTextNormalizationSmokeTest;
    }

    private bool HandleRuntimeTextNormalizationSmokeTest(string[] args)
    {
        try
        {
            RunRuntimeTextNormalizationSmoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runtime text normalization smoke failed: {ex}");
            Environment.ExitCode = 1;
        }

        return true;
    }

    private static void RunRuntimeTextNormalizationSmoke()
    {
        Assembly assembly = typeof(DeveloperCommandHandler).Assembly;
        string root = FindRuntimeTextNormalizationRepositoryRoot();

        RequireMcpToolDescriptionsAreEnglish(assembly);
        RequireRequestDescriptionsAreEnglish(root);
        RequireTargetedSourceFilesAreEnglish(root);

        Console.WriteLine("[OK] Runtime text normalization smoke verified targeted MCP descriptions, request descriptions, formatter, validator, and CLI text.");
    }

    private static void RequireMcpToolDescriptionsAreEnglish(Assembly assembly)
    {
        foreach (Type type in GetRuntimeTextNormalizationLoadableTypes(assembly)
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null))
            {
                string description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
                RequireNoCjk(description, $"MCP tool description {type.Name}.{method.Name}");
            }
        }
    }

    private static void RequireRequestDescriptionsAreEnglish(string root)
    {
        string requestsRoot = Path.Combine(root, "src", "MCP_Rhino.Server", "Contracts", "Requests");
        RequireRuntimeText(Directory.Exists(requestsRoot), $"Contracts request directory was not found: {requestsRoot}");

        foreach (string path in Directory.EnumerateFiles(requestsRoot, "*.cs", SearchOption.TopDirectoryOnly))
        {
            RequireNoCjk(File.ReadAllText(path), Path.GetRelativePath(root, path));
        }
    }

    private static void RequireTargetedSourceFilesAreEnglish(string root)
    {
        string[] relativePaths =
        {
            Path.Combine("src", "MCP_Rhino.Server", "Contracts", "Requests", "ObjectScopedUserTextKeyRequest.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Contracts", "Requests", "ObjectScopedUserTextEntryRequest.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Contracts", "Requests", "DocumentUserStringEntryRequest.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "PassThroughEditResultFormatter.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "Rhino", "Live", "LiveRhinoGeometryValidator.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "CLI", "DeveloperCommandHandler.cs"),
            Path.Combine("src", "MCP_Rhino.Server", "Infrastructure", "CLI", "DeveloperCommandHandler.Parsing.cs")
        };

        foreach (string relativePath in relativePaths)
        {
            string path = Path.Combine(root, relativePath);
            RequireRuntimeText(File.Exists(path), $"Targeted source file was not found: {relativePath}");
            RequireNoCjk(File.ReadAllText(path), relativePath);
        }
    }

    private static IEnumerable<Type> GetRuntimeTextNormalizationLoadableTypes(Assembly assembly)
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

    private static string FindRuntimeTextNormalizationRepositoryRoot()
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

    private static void RequireNoCjk(string text, string label)
    {
        Match match = RuntimeTextCjkPattern.Match(text);
        RequireRuntimeText(!match.Success, $"{label} contains CJK/fullwidth runtime text near [{match.Value}].");
    }

    private static void RequireRuntimeText(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
