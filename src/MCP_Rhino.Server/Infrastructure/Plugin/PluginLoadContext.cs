extern alias rhinocommon;

using System.Reflection;
using System.Runtime.Loader;
using RhinoApp = rhinocommon::Rhino.RhinoApp;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

// Isolates MCP SDK loading so that System.Text.Json 10.x (and its transitive chain
// via Microsoft.Extensions.AI) resolves from the plugin bin instead of the older
// System.Text.Json 7.0 that Rhino 8 preloads into AssemblyLoadContext.Default.
// RhinoCommon must fall back to the default context; otherwise tools and runtime
// bootstrap would see duplicate Rhino.Geometry.* / Rhino.FileIO.* types.
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string mainAssemblyPath)
        : base(name: "MCP_Rhino", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "RhinoCommon" or "Rhino.UI" or "Eto" or "Eto.Wpf" or "Grasshopper" or "GH_IO")
        {
            RhinoApp.WriteLine($"[MCP_Rhino diag] ALC.Load({assemblyName.Name}) -> share with default context");
            return null;
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            if (IsInteresting(assemblyName.Name))
            {
                RhinoApp.WriteLine($"[MCP_Rhino diag] ALC.Load({assemblyName.Name}) -> resolver returned null, falling back to default context");
            }
            return null;
        }

        if (IsInteresting(assemblyName.Name))
        {
            RhinoApp.WriteLine($"[MCP_Rhino diag] ALC.Load({assemblyName.Name}) -> {path}");
        }
        return LoadFromAssemblyPath(path);
    }

    private static bool IsInteresting(string? name) =>
        name is "System.Text.Json"
             or "System.Text.Encodings.Web"
             or "Microsoft.Extensions.AI"
             or "Microsoft.Extensions.AI.Abstractions"
             or "Microsoft.Extensions.Hosting"
             or "Grasshopper"
             or "GH_IO"
             or "ModelContextProtocol"
             or "ModelContextProtocol.Core"
             or "MCP_Rhino.Server.Runtime";
}
