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
    private readonly object _boundPipeLock = new();
    private readonly Dictionary<string, McpNamedPipeServer> _boundPipeServers = new(StringComparer.OrdinalIgnoreCase);
    private McpNamedPipeServer? _pipeServer;

    public void Start(string pipeName)
    {
        _pipeServer = new McpNamedPipeServer(
            pipeName,
            CreateConnectionHost,
            stopOnPipeCreateFailure: true,
            pipeCreateFailureHint: "If another Rhino instance owns this debug pipe, this Rhino instance can still use process-scoped panel pipes.");
        _pipeServer.Start();
    }

    public void StartBoundPipeServer(string pipeName, uint runtimeSerialNumber)
    {
        lock (_boundPipeLock)
        {
            if (_boundPipeServers.ContainsKey(pipeName))
            {
                return;
            }

            var pipeServer = new McpNamedPipeServer(
                pipeName,
                BoundHostFactory.For(runtimeSerialNumber),
                stopOnPipeCreateFailure: true,
                pipeCreateFailureHint: "This panel-bound pipe did not start. Check for stale clients or unexpected pipe-name collisions.");
            pipeServer.Start();
            _boundPipeServers.Add(pipeName, pipeServer);
        }
    }

    public void StopBoundPipeServer(string pipeName)
    {
        McpNamedPipeServer? pipeServer = null;

        lock (_boundPipeLock)
        {
            if (_boundPipeServers.Remove(pipeName, out McpNamedPipeServer? existing))
            {
                pipeServer = existing;
            }
        }

        pipeServer?.Dispose();
    }

    public void Dispose()
    {
        List<McpNamedPipeServer> boundServers;
        lock (_boundPipeLock)
        {
            boundServers = _boundPipeServers.Values.ToList();
            _boundPipeServers.Clear();
        }

        foreach (McpNamedPipeServer boundServer in boundServers)
        {
            boundServer.Dispose();
        }

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
