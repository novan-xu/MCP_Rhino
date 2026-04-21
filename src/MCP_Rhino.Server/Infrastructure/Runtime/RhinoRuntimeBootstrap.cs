using System.Reflection;
using System.Runtime.InteropServices;
using Rhino3dmAssemblyMarker = Rhino.FileIO.File3dm;

namespace MCP_Rhino.Server.Infrastructure.Runtime;

// Centralizes CLR resolution hooks required for the server to run in two distinct hosts:
//   (1) plugin mode — loaded by Rhino from the .rhp output directory
//   (2) CLI mode   — launched via dotnet run / dotnet exec, Rhino not in process
// Both hosts need Rhino3dm's native dependency librhino3dm_native resolved from the
// plugin's own runtimes/<rid>/native/ folder; CLI additionally needs the managed
// RhinoCommon reference resolved from Rhino's install dir since Private=false keeps
// it out of the output directory (a hard requirement for .rhp loading).
public static class RhinoRuntimeBootstrap
{
    private const string RhinoNetcoreDirectory = @"C:\Program Files\Rhino 8\System\netcore";
    private const string RhinoCommonAssemblyName = "RhinoCommon";
    private const string Rhino3dmNativeLibraryName = "librhino3dm_native";

    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += TryResolveRhinoCommon;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(Rhino3dmAssemblyMarker).Assembly, ResolveRhino3dmNative);
        }
        catch (InvalidOperationException)
        {
            // A resolver for this assembly was already registered (e.g. the plugin host
            // bootstrapped before CLI bootstrap ran). Subsequent sets throw; we treat
            // the first registration as authoritative.
        }
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

    private static IntPtr ResolveRhino3dmNative(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(Rhino3dmNativeLibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        string? pluginDirectory = GetPluginDirectory();
        if (pluginDirectory is null)
        {
            return IntPtr.Zero;
        }

        string rid = GetRuntimeIdentifier();
        string fileName = GetNativeFileName();
        string candidate = Path.Combine(pluginDirectory, "runtimes", rid, "native", fileName);

        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    private static string? GetPluginDirectory()
    {
        string? assemblyLocation = typeof(RhinoRuntimeBootstrap).Assembly.Location;
        if (string.IsNullOrEmpty(assemblyLocation))
        {
            return null;
        }

        return Path.GetDirectoryName(assemblyLocation);
    }

    private static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
    }

    private static string GetNativeFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "librhino3dm_native.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "librhino3dm_native.dylib";
        return "librhino3dm_native.so";
    }
}
