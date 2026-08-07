using Grasshopper;
using Grasshopper.Kernel;
using MCP_Rhino.Server.Contracts.Responses;

namespace MCP_Rhino.Server.Infrastructure.Rhino.Live.Grasshopper;

public sealed class GrasshopperComponentCatalog
{
    public IReadOnlyList<IGH_ObjectProxy> Find(string query)
    {
        string normalized = query.Trim();
        return Instances.ComponentServer.ObjectProxies
            .Where(proxy => !proxy.Obsolete && !IsHidden(proxy))
            .Where(proxy => Matches(proxy, normalized))
            .OrderBy(proxy => proxy.Desc.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(proxy => proxy.Desc.SubCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(proxy => proxy.Desc.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public OperationResult Resolve(Guid? componentGuid, string? componentName)
    {
        if (componentGuid is Guid exact)
        {
            IGH_ObjectProxy? proxy = Instances.ComponentServer.EmitObjectProxy(exact);
            return proxy is null || proxy.Obsolete || IsHidden(proxy)
                ? OperationResult.NotFound()
                : OperationResult.Success(proxy);
        }

        string name = componentName?.Trim() ?? string.Empty;
        List<IGH_ObjectProxy> matches = Find(name)
            .Where(proxy => string.Equals(proxy.Desc.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(proxy.Desc.NickName, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            0 => OperationResult.NotFound(),
            1 => OperationResult.Success(matches[0]),
            _ => OperationResult.Ambiguous(matches)
        };
    }

    public GrasshopperComponentSummary ToSummary(IGH_ObjectProxy proxy)
    {
        GH_AssemblyInfo? assembly = Instances.ComponentServer.FindAssembly(proxy.LibraryGuid);
        (bool executable, string? host) = ClassifyExecutableCode(proxy);
        return new GrasshopperComponentSummary
        {
            ComponentGuid = proxy.Guid,
            Name = proxy.Desc.Name ?? string.Empty,
            NickName = proxy.Desc.NickName ?? string.Empty,
            Category = proxy.Desc.Category ?? string.Empty,
            SubCategory = proxy.Desc.SubCategory ?? string.Empty,
            Description = proxy.Desc.Description ?? string.Empty,
            ObjectKind = proxy.Kind.ToString(),
            LibraryGuid = proxy.LibraryGuid,
            AssemblyName = assembly?.AssemblyName ?? proxy.Type?.Assembly.GetName().Name ?? string.Empty,
            AssemblyVersion = assembly?.AssemblyVersion ?? proxy.Type?.Assembly.GetName().Version?.ToString() ?? string.Empty,
            Hidden = IsHidden(proxy),
            Obsolete = proxy.Obsolete,
            IsExecutableCodeComponent = executable,
            CodeLanguageOrHost = host
        };
    }

    public static (bool IsExecutable, string? Host) ClassifyExecutableCode(IGH_ObjectProxy proxy)
    {
        string text = string.Join(" ",
            proxy.Type?.FullName,
            proxy.Desc.Name,
            proxy.Desc.NickName,
            proxy.Desc.Category,
            proxy.Desc.SubCategory);

        if (text.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Python");
        }

        if (text.Contains("csharp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("c#", StringComparison.OrdinalIgnoreCase)
            || text.Contains("scriptcomponent", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "C#/.NET script host");
        }

        if (text.Contains("script", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Script host");
        }

        return (false, null);
    }

    public static bool IsExecutableCodeObject(IGH_DocumentObject documentObject)
    {
        IGH_ObjectProxy? proxy = Instances.ComponentServer.EmitObjectProxy(documentObject.ComponentGuid);
        return proxy is not null && ClassifyExecutableCode(proxy).IsExecutable;
    }

    private static bool IsHidden(IGH_ObjectProxy proxy) => proxy.Exposure == GH_Exposure.hidden;

    private static bool Matches(IGH_ObjectProxy proxy, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        return Contains(proxy.Desc.Name, query)
            || Contains(proxy.Desc.NickName, query)
            || Contains(proxy.Desc.Description, query)
            || Contains(proxy.Desc.Category, query)
            || Contains(proxy.Desc.SubCategory, query);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    public sealed class OperationResult
    {
        public IGH_ObjectProxy? Proxy { get; init; }
        public IReadOnlyList<IGH_ObjectProxy> Candidates { get; init; } = [];
        public bool IsAmbiguous => Candidates.Count > 1;

        public static OperationResult Success(IGH_ObjectProxy proxy) => new() { Proxy = proxy };
        public static OperationResult NotFound() => new();
        public static OperationResult Ambiguous(IReadOnlyList<IGH_ObjectProxy> candidates) => new() { Candidates = candidates };
    }
}
