extern alias rhinocommon;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using MCP_Rhino.Server.Infrastructure.CLI;
using MCP_Rhino.Server.Infrastructure.ClaudeCode;
using MCP_Rhino.Server.Infrastructure.Plugin.Companion;
using MCP_Rhino.Server.Infrastructure.Plugin.Panel;
using MCP_Rhino.Server.Infrastructure.Runtime;
using MCP_Rhino.Server.Server;
using LoadReturnCode = rhinocommon::Rhino.PlugIns.LoadReturnCode;
using PlugIn = rhinocommon::Rhino.PlugIns.PlugIn;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

[Guid(PluginIdText)]
public sealed class McpRhinoPlugin : PlugIn
{
    internal const string PluginIdText = "7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A";
    internal const string PipeName = McpPipeNames.DeveloperDebugPipeName;
    internal static readonly Guid PluginId = new(PluginIdText);
#if MCP_RHINO_BRIDGE_PIPE_ONLY
    internal static bool IsBridgePipeOnlyBuild => true;
#else
    internal static bool IsBridgePipeOnlyBuild => false;
#endif

    public static McpRhinoPlugin? Instance { get; private set; }

    private AssemblyLoadContext? _isolatedContext;
    private Type? _bootstrapType;
    private object? _serverHandle;
    private PerDocumentPanelDispatcher? _panelDispatcher;
    private string? _pluginDirectory;
    private bool _panelRegistered;
    private readonly Dictionary<uint, CompanionSessionHandle> _companionSessions = new();

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
            _pluginDirectory = pluginDir;

            string pluginDllPath = Path.Combine(pluginDir, "MCP_Rhino.Server.dll");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Plugin dir: {pluginDir}");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Plugin DLL exists: {File.Exists(pluginDllPath)}");

            _isolatedContext = new PluginLoadContext(pluginDllPath);
            Assembly isolatedAssembly = _isolatedContext.LoadFromAssemblyPath(pluginDllPath);
            RhinoApp.WriteLine($"[MCP_Rhino diag] Loaded isolated assembly: {isolatedAssembly.FullName}");
            RhinoApp.WriteLine($"[MCP_Rhino diag] Isolated ALC: {AssemblyLoadContext.GetLoadContext(isolatedAssembly)?.Name}");

            _bootstrapType = isolatedAssembly.GetType(
                "MCP_Rhino.Server.Infrastructure.Plugin.ServerBootstrap",
                throwOnError: true)!;

            _serverHandle = Activator.CreateInstance(_bootstrapType)
                ?? throw new InvalidOperationException("Failed to create ServerBootstrap instance.");
            _bootstrapType.GetMethod("Start")!.Invoke(_serverHandle, new object[] { PipeName });

            if (!IsBridgePipeOnlyBuild)
            {
                RhinoDoc.CloseDocument += OnCloseDocumentForCompanion;
            }

            RhinoApp.WriteLine($"MCP_Rhino plugin loaded. Developer debug pipe requested: \\\\.\\pipe\\{PipeName}");
            if (IsBridgePipeOnlyBuild)
            {
                RhinoApp.WriteLine("MCP_Rhino Debug bridge-only plugin loaded. Chat panel, Companion, and panel-bound pipes are disabled in this build.");
            }
            else
            {
                RhinoApp.WriteLine($"MCP_Rhino panel pipes are process-scoped for Rhino PID {Environment.ProcessId}.");
            }
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
        RhinoDoc.CloseDocument -= OnCloseDocumentForCompanion;
        StopAllCompanions();

        _panelDispatcher?.Dispose();
        _panelDispatcher = null;
        _panelRegistered = false;
        _pluginDirectory = null;

        if (_serverHandle is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _serverHandle = null;
        _bootstrapType = null;

        _isolatedContext?.Unload();
        _isolatedContext = null;

        RhinoApp.WriteLine("MCP_Rhino plugin unloaded.");
        base.OnShutdown();
    }

    internal void StartBoundPipeServer(string pipeName, uint runtimeSerialNumber)
    {
        if (IsBridgePipeOnlyBuild)
        {
            throw new InvalidOperationException("Debug bridge-only plugin does not start panel-bound MCP pipes. Use the Release plugin for chat panel sessions.");
        }

        if (_bootstrapType is null || _serverHandle is null)
        {
            throw new InvalidOperationException("Server bootstrap is not available.");
        }

        _bootstrapType.GetMethod("StartBoundPipeServer")!.Invoke(_serverHandle, new object[] { pipeName, runtimeSerialNumber });
    }

    internal void StopBoundPipeServer(string pipeName)
    {
        if (_bootstrapType is null || _serverHandle is null)
        {
            return;
        }

        _bootstrapType.GetMethod("StopBoundPipeServer")!.Invoke(_serverHandle, new object[] { pipeName });
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

    internal bool TryShowChatPanel(RhinoDoc document)
    {
        if (IsBridgePipeOnlyBuild)
        {
            RhinoApp.WriteLine("This Debug plugin is bridge-pipe-only. Use MCP_Rhino.Bridge.exe with \\\\.\\pipe\\mcp_rhino, or load the Release plugin for _Mcpchat.");
            return false;
        }

        if (TryShowCompanion(document))
        {
            return true;
        }

        RhinoApp.WriteLine("MCP_Rhino Companion is not available. Falling back to the Rhino-hosted chat panel.");
        return TryShowEtoPanel(document);
    }

    private bool TryShowCompanion(RhinoDoc document)
    {
        if (string.IsNullOrWhiteSpace(_pluginDirectory))
        {
            RhinoApp.WriteLine("MCP_Rhino plugin directory is not available. Reload the plugin and try _Mcpchat again.");
            return false;
        }

        string? companionPath = CompanionProcessLauncher.FindCompanionExecutable(_pluginDirectory);
        if (string.IsNullOrWhiteSpace(companionPath))
        {
            RhinoApp.WriteLine("MCP_Rhino Companion executable was not found. Build src\\MCP_Rhino.Companion first.");
            return false;
        }

        string? bridgePath = McpConfigBuilder.FindBridgeExecutable(_pluginDirectory);
        if (string.IsNullOrWhiteSpace(bridgePath))
        {
            RhinoApp.WriteLine("MCP_Rhino Bridge executable was not found. Build src\\MCP_Rhino.Bridge first.");
            return false;
        }

        uint serial = document.RuntimeSerialNumber;
        string documentPath = document.Path;
        if (_companionSessions.TryGetValue(serial, out CompanionSessionHandle? existing))
        {
            if (existing.IsRunning
                && string.Equals(existing.Spec.DocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
            {
                existing.Focus();
                RhinoApp.WriteLine($"MCP_Rhino Companion focused for {Path.GetFileName(documentPath)}.");
                return true;
            }

            StopCompanion(serial);
        }

        string pipeName = McpPipeNames.ForPanelBoundDocument(serial);
        StartBoundPipeServer(pipeName, serial);

        var spec = new CompanionLaunchSpec(
            companionPath,
            documentPath,
            serial,
            pipeName,
            bridgePath);

        try
        {
            CompanionSessionHandle handle = CompanionProcessLauncher.Start(spec);
            _companionSessions[serial] = handle;
            RhinoApp.WriteLine($"MCP_Rhino Companion started for {Path.GetFileName(documentPath)}. Pipe: {pipeName}");
            return true;
        }
        catch (Exception ex)
        {
            StopBoundPipeServer(pipeName);
            RhinoApp.WriteLine($"MCP_Rhino Companion failed to start: {ex}");
            return false;
        }
    }

    private bool TryShowEtoPanel(RhinoDoc document)
    {
        if (_panelDispatcher is null)
        {
            if (string.IsNullOrWhiteSpace(_pluginDirectory))
            {
                RhinoApp.WriteLine("MCP_Rhino plugin directory is not available. Reload the plugin and try _Mcpchat again.");
                return false;
            }

            RhinoApp.WriteLine("MCP_Rhino Claude Code panel dispatcher was not started; retrying panel startup.");
            TryStartPanelDispatcher(_pluginDirectory, startOpenDocuments: false);
        }

        return _panelDispatcher?.TryShowDocument(document) ?? false;
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
            .AddLiveRhinoAdapters()
            .AddRhinoApplication()
            .AddRhinoAgents();
    }

    private void TryStartPanelDispatcher(string pluginDir, bool startOpenDocuments)
    {
        if (_panelDispatcher is not null)
        {
            return;
        }

        try
        {
            if (!_panelRegistered)
            {
                PerDocumentPanelDispatcher.RegisterPanel(this, PluginId);
                _panelRegistered = true;
            }

            var host = new RhinoChatPanelHost(pluginDir, StartBoundPipeServer, StopBoundPipeServer);
            _panelDispatcher = new PerDocumentPanelDispatcher(host);
            _panelDispatcher.Start(startOpenDocuments);
            RhinoApp.WriteLine("MCP_Rhino Claude Code panel dispatcher started.");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP_Rhino Claude Code panel dispatcher failed to start: {ex}");
        }
    }

    private void OnCloseDocumentForCompanion(object? sender, EventArgs e)
    {
        RhinoDoc? document = GetDocument(sender, e);
        if (document is null)
        {
            return;
        }

        StopCompanion(document.RuntimeSerialNumber);
    }

    private void StopCompanion(uint runtimeSerialNumber)
    {
        if (!_companionSessions.Remove(runtimeSerialNumber, out CompanionSessionHandle? handle))
        {
            return;
        }

        handle.Dispose();
        StopBoundPipeServer(handle.Spec.PipeName);
    }

    private void StopAllCompanions()
    {
        foreach (uint serial in _companionSessions.Keys.ToArray())
        {
            StopCompanion(serial);
        }
    }

    private static RhinoDoc? GetDocument(object? sender, EventArgs e)
    {
        if (sender is RhinoDoc senderDocument)
        {
            return senderDocument;
        }

        object? document = e.GetType().GetProperty("Document")?.GetValue(e);
        return document as RhinoDoc;
    }
}
