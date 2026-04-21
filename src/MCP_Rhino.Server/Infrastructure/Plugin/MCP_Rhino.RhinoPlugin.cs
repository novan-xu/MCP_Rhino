extern alias rhinocommon;

using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    private McpNamedPipeServer? _pipeServer;

    public McpRhinoPlugin()
    {
        Instance = this;
    }

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        try
        {
            RhinoRuntimeBootstrap.Initialize();
            _pipeServer = new McpNamedPipeServer(PipeName, CreateConnectionHost);
            _pipeServer.Start();
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
        _pipeServer?.Dispose();
        _pipeServer = null;
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

    private static IHost CreateConnectionHost(Stream input, Stream output)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ConfigurePluginServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .AddRhinoTools()
            .WithStreamServerTransport(input, output);

        return builder.Build();
    }

    private static IServiceCollection CreatePluginServices()
    {
        var services = new ServiceCollection();
        ConfigurePluginServices(services);
        return services;
    }

    private static void ConfigurePluginServices(IServiceCollection services)
    {
        services
            .AddOfflineRhinoAdapters()
            .AddLiveRhinoAdapters()
            .AddRhinoApplication()
            .AddRhinoAgents();
    }
}
