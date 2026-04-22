using MCP_Rhino.Server.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

// Entry point invoked by McpRhinoPlugin via reflection after this assembly has
// been reloaded into an isolated AssemblyLoadContext. All MCP SDK types and
// their System.Text.Json 10.x dependency resolve within that context; the plugin
// class itself stays in AssemblyLoadContext.Default so Rhino's PlugInManager
// keeps a single, stable PlugIn instance.
public sealed class ServerBootstrap : IDisposable
{
    private McpNamedPipeServer? _pipeServer;

    public void Start(string pipeName)
    {
        _pipeServer = new McpNamedPipeServer(pipeName, CreateConnectionHost);
        _pipeServer.Start();
    }

    public void Dispose()
    {
        _pipeServer?.Dispose();
        _pipeServer = null;
    }

    private static IHost CreateConnectionHost(Stream input, Stream output)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        McpRhinoPlugin.ConfigurePluginServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .AddRhinoTools()
            .WithStreamServerTransport(input, output);

        return builder.Build();
    }
}
