extern alias rhinocommon;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Runtime;
using MCP_Rhino.Server.Server;
using LoadReturnCode = rhinocommon::Rhino.PlugIns.LoadReturnCode;
using PlugIn = rhinocommon::Rhino.PlugIns.PlugIn;
using RhinoApp = rhinocommon::Rhino.RhinoApp;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

[Guid("7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A")]
public sealed class McpRhinoPlugin : PlugIn
{
    internal const string PipeName = "mcp_rhino";

    public static McpRhinoPlugin? Instance { get; private set; }

    private AssemblyLoadContext? _isolatedContext;
    private object? _serverHandle;

    public McpRhinoPlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            RhinoApp.WriteLine("[MCP_Rhino diag] OnLoad v2 (isolated-context) starting.");

            RhinoRuntimeBootstrap.Initialize();

            string pluginDir = Path.GetDirectoryName(typeof(McpRhinoPlugin).Assembly.Location)
                ?? throw new InvalidOperationException("Unable to determine plugin assembly directory.");
            string pluginDllPath = Path.Combine(pluginDir, "MCP_Rhino.Server.dll");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Plugin dir: {pluginDir}");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Plugin DLL exists: {File.Exists(pluginDllPath)}");

            _isolatedContext = new PluginLoadContext(pluginDllPath);
            Assembly isolatedAssembly = _isolatedContext.LoadFromAssemblyPath(pluginDllPath);
            RhinoApp.WriteLine($"[MCP_Rhino diag] Loaded isolated assembly: {isolatedAssembly.FullName}");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Isolated ALC: {AssemblyLoadContext.GetLoadContext(isolatedAssembly)?.Name}");

            Type bootstrapType = isolatedAssembly.GetType(
                "MCP_Rhino.Server.Infrastructure.Plugin.ServerBootstrap",
                throwOnError: true)!;

            _serverHandle = Activator.CreateInstance(bootstrapType)
                ?? throw new InvalidOperationException("Failed to create ServerBootstrap instance.");
            bootstrapType.GetMethod("Start")!.Invoke(_serverHandle, new object[] { PipeName });

            RhinoApp.WriteLine($"MCP_Rhino plugin loaded. Named pipe ready: \\\\.\\pipe\\{PipeName}");
            return LoadReturnCode.Success;
        }
        catch (Exception ex)
        {
            errorMessage = ex.ToString();
            RhinoApp.WriteLine($"MCP_Rhino plugin failed to load: {ex}");
            return LoadReturnCode.ErrorShowDialog;
        }
    }

    protected override void OnShutdown()
    {
        if (_serverHandle is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _serverHandle = null;

        _isolatedContext?.Unload();
        _isolatedContext = null;

        RhinoApp.WriteLine("MCP_Rhino plugin unloaded.");
        base.OnShutdown();
    }

    internal bool RunDeveloperCommand(params string[] args)
    {
        using ServiceProvider provider = CreatePluginServices().BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        DeveloperCommandHandler handler = scope.ServiceProvider.GetRequiredService<DeveloperCommandHandler>();

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        TextWriter originalOut = Console.Out;
        TextWriter originalErr = Console.Error;
        int originalExitCode = System.Environment.ExitCode;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            System.Environment.ExitCode = 0;

            bool handled = handler.TryHandle(args);
            string standardOutput = stdout.ToString().Trim();
            string standardError = stderr.ToString().Trim();

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                RhinoApp.WriteLine(standardOutput);
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                RhinoApp.WriteLine(standardError);
            }

            return handled && System.Environment.ExitCode == 0;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            System.Environment.ExitCode = originalExitCode;
        }
    }

    private static IServiceCollection CreatePluginServices()
    {
        var services = new ServiceCollection();
        ConfigurePluginServices(services);
        return services;
    }

    internal static void ConfigurePluginServices(IServiceCollection services)
    {
        services
            .AddOfflineRhinoAdapters()
            .AddLiveRhinoAdapters()
            .AddRhinoApplication()
            .AddRhinoAgents();
    }
}
