using System.Reflection;

namespace MCP_Rhino.Server.Infrastructure.Runtime;

// Centralizes CLR resolution hooks required for the server to run in two distinct hosts:
//   (1) plugin mode — loaded by Rhino from the .rhp output directory
//   (2) CLI mode   — launched via dotnet run / dotnet exec, Rhino not in process
// CLI additionally needs the managed RhinoCommon reference resolved from Rhino's
// install dir since Private=false keeps it out of the output directory (a hard
// requirement for .rhp loading).
public static class RhinoRuntimeBootstrap
{
    private const string RhinoNetcoreDirectory = @"C:\Program Files\Rhino 8\System\netcore";
    private const string RhinoCommonAssemblyName = "RhinoCommon";

    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += TryResolveRhinoCommon;
    }

    private static Assembly? TryResolveRhinoCommon(object? sender, ResolveEventArgs args)
    {
        string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;
        if (!string.Equals(assemblyName, RhinoCommonAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string candidate = Path.Combine(RhinoNetcoreDirectory, "RhinoCommon.dll");
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
