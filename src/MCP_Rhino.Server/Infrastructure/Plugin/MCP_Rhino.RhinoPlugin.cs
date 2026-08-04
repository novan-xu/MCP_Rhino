extern alias rhinocommon;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.Plugin.Routing;
using MCP_Rhino.Server.Infrastructure.Runtime;
using MCP_Rhino.Server.Server;
using LoadReturnCode = rhinocommon::Rhino.PlugIns.LoadReturnCode;
using PlugIn = rhinocommon::Rhino.PlugIns.PlugIn;
using PlugInLoadTime = rhinocommon::Rhino.PlugIns.PlugInLoadTime;
using RhinoApp = rhinocommon::Rhino.RhinoApp;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

[Guid(PluginIdText)]
public sealed class McpRhinoPlugin : PlugIn
{
    internal const string PluginIdText = "7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A";
    internal static readonly Guid PluginId = new(PluginIdText);

    public static McpRhinoPlugin? Instance { get; private set; }

    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    private AssemblyLoadContext? _isolatedContext;
    private Type? _bootstrapType;
    private object? _serverHandle;
    private RoutedDocumentEndpointDispatcher? _routedEndpointDispatcher;

    public McpRhinoPlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            RhinoApp.WriteLine("[MCP_Rhino diag] Router-only plugin startup beginning.");
            RhinoRuntimeBootstrap.Initialize();

            string pluginDir = Path.GetDirectoryName(typeof(McpRhinoPlugin).Assembly.Location)
                ?? throw new InvalidOperationException("Unable to determine plugin assembly directory.");
            string runtimeDllPath = Path.Combine(pluginDir, "MCP_Rhino.Server.Runtime.dll");

            _isolatedContext = new PluginLoadContext(runtimeDllPath);
            Assembly isolatedAssembly = _isolatedContext.LoadFromAssemblyPath(runtimeDllPath);
            _bootstrapType = isolatedAssembly.GetType(
                "MCP_Rhino.Server.Infrastructure.Plugin.ServerBootstrap",
                throwOnError: true)!;
            _serverHandle = Activator.CreateInstance(_bootstrapType)
                ?? throw new InvalidOperationException("Failed to create ServerBootstrap instance.");

            TryStartRoutedEndpointDispatcher();
            RhinoApp.WriteLine($"MCP_Rhino Router-only plugin loaded for Rhino PID {Environment.ProcessId}.");
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
        _routedEndpointDispatcher?.Dispose();
        _routedEndpointDispatcher = null;

        if (_serverHandle is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serverHandle = null;
        _bootstrapType = null;
        _isolatedContext?.Unload();
        _isolatedContext = null;

        RhinoApp.WriteLine("MCP_Rhino Router-only plugin unloaded.");
        base.OnShutdown();
    }

    internal bool StartRoutedPipeServer(string pipeName, uint runtimeSerialNumber, string attestationJson)
    {
        if (_bootstrapType is null || _serverHandle is null)
        {
            return false;
        }

        object? result = _bootstrapType.GetMethod("StartRoutedPipeServer")!.Invoke(
            _serverHandle,
            new object[] { pipeName, runtimeSerialNumber, attestationJson });
        return result is true;
    }

    internal void UpdateRoutedPipeAttestation(string pipeName, string attestationJson)
    {
        if (_bootstrapType is null || _serverHandle is null)
        {
            return;
        }

        _bootstrapType.GetMethod("UpdateRoutedPipeAttestation")!.Invoke(
            _serverHandle,
            new object[] { pipeName, attestationJson });
    }

    internal void StopRoutedPipeServer(string pipeName)
    {
        if (_bootstrapType is null || _serverHandle is null)
        {
            return;
        }

        _bootstrapType.GetMethod("StopRoutedPipeServer")!.Invoke(_serverHandle, new object[] { pipeName });
    }

    internal void StopAllRoutedPipeServers()
    {
        if (_bootstrapType is null || _serverHandle is null)
        {
            return;
        }

        _bootstrapType.GetMethod("StopAllRoutedPipeServers")!.Invoke(_serverHandle, Array.Empty<object>());
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
        int originalExitCode = Environment.ExitCode;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Environment.ExitCode = 0;

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

            return handled && Environment.ExitCode == 0;
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Environment.ExitCode = originalExitCode;
        }
    }

    internal static void ConfigurePluginServices(IServiceCollection services)
    {
        PluginServiceRegistration.Configure(services);
    }

    private static IServiceCollection CreatePluginServices()
    {
        var services = new ServiceCollection();
        PluginServiceRegistration.Configure(services);
        return services;
    }

    private void TryStartRoutedEndpointDispatcher()
    {
        if (_routedEndpointDispatcher is not null)
        {
            return;
        }

        try
        {
            _routedEndpointDispatcher = new RoutedDocumentEndpointDispatcher(this);
            _routedEndpointDispatcher.Start();
            RhinoApp.WriteLine("MCP_Rhino routed document endpoint dispatcher started.");
        }
        catch (Exception ex)
        {
            _routedEndpointDispatcher?.Dispose();
            _routedEndpointDispatcher = null;
            RhinoApp.WriteLine($"MCP_Rhino routed endpoint dispatcher is unavailable: {ex}");
        }
    }
}
