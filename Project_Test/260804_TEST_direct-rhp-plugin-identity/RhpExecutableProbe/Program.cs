using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

if (args.Length == 3 && string.Equals(args[0], "--rhino-identities", StringComparison.Ordinal))
{
    string rhpPath = Path.GetFullPath(args[1]);
    string rhinoCommonPath = Path.GetFullPath(args[2]);
    Assembly rhinoCommon = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
        string.Equals(assembly.GetName().Name, "RhinoCommon", StringComparison.OrdinalIgnoreCase))
        ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(rhinoCommonPath);

    var context = new RhinoPluginProbeLoadContext(Path.GetDirectoryName(rhpPath)!, rhinoCommon);
    Assembly pluginAssembly = context.LoadFromAssemblyPath(rhpPath);
    Type commandBase = rhinoCommon.GetType("Rhino.Commands.Command", throwOnError: true)!;
    Type pluginBase = rhinoCommon.GetType("Rhino.PlugIns.PlugIn", throwOnError: true)!;

    foreach (Type type in pluginAssembly.GetTypes()
        .Where(type => !type.IsAbstract && (commandBase.IsAssignableFrom(type) || pluginBase.IsAssignableFrom(type)))
        .OrderBy(type => type.FullName, StringComparer.Ordinal))
    {
        string kind = commandBase.IsAssignableFrom(type) ? "COMMAND" : "PLUGIN";
        GuidAttribute? explicitGuid = type.GetCustomAttribute<GuidAttribute>();
        Console.WriteLine(
            $"RHINO_IDENTITY|kind={kind}|guid={type.GUID:D}|explicit={explicitGuid is not null}|type={type.FullName}");
    }

    context.Unload();
    return;
}

if (args.Length > 0)
{
    foreach (string input in args)
    {
        string path = Path.GetFullPath(input);
        using FileStream stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        int entryPoint = reader.PEHeaders.CorHeader?.EntryPointTokenOrRelativeVirtualAddress ?? 0;
        Console.WriteLine($"PE_ENTRYPOINT|path={path}|token={entryPoint}");
    }

    return;
}

Console.WriteLine($"RHP_EXECUTABLE_PROBE_OK|location={typeof(Program).Assembly.Location}");

sealed class RhinoPluginProbeLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;
    private readonly Assembly _rhinoCommon;

    public RhinoPluginProbeLoadContext(string pluginDirectory, Assembly rhinoCommon)
        : base(nameof(RhinoPluginProbeLoadContext), isCollectible: true)
    {
        _pluginDirectory = pluginDirectory;
        _rhinoCommon = rhinoCommon;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, _rhinoCommon.GetName().Name, StringComparison.OrdinalIgnoreCase))
        {
            return _rhinoCommon;
        }

        string candidate = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
