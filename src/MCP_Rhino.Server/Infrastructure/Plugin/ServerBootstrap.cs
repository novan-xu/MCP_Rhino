using MCP_Rhino.Server.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MCP_Rhino.Server.Infrastructure.Plugin;

// Entry point invoked by McpRhinoPlugin via reflection after this assembly has
// been reloaded into an isolated AssemblyLoadContext. Only primitives and JSON
// strings cross the load-context boundary.
public sealed class ServerBootstrap : IDisposable
{
    private static readonly TimeSpan ListenerReadyTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AggregateStopTimeout = TimeSpan.FromSeconds(5);

    private readonly object _boundPipeLock = new();
    private readonly Dictionary<string, McpNamedPipeServer> _boundPipeServers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _routedPipeLock = new();
    private readonly Dictionary<string, RoutedPipeRegistration> _routedPipeServers = new(StringComparer.OrdinalIgnoreCase);
    private McpNamedPipeServer? _pipeServer;

    public void Start(string pipeName)
    {
        _pipeServer = new McpNamedPipeServer(
            pipeName,
            CreateConnectionHost,
            stopOnPipeCreateFailure: true,
            pipeCreateFailureHint: "If another Rhino instance owns this debug pipe, this Rhino instance can still use process-scoped panel and route pipes.");
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

    public bool StartRoutedPipeServer(string pipeName, uint runtimeSerialNumber, string attestationJson)
    {
        lock (_routedPipeLock)
        {
            if (_routedPipeServers.ContainsKey(pipeName))
            {
                return true;
            }

            var attestationProvider = new RoutedAttestationProvider(attestationJson);
            var pipeServer = new McpNamedPipeServer(
                pipeName,
                RoutedHostFactory.For(runtimeSerialNumber, attestationProvider),
                stopOnPipeCreateFailure: true,
                pipeCreateFailureHint: "This route pipe did not start. The document remains unavailable to external MCP routers.",
                maxConcurrentConnections: 8,
                currentUserOnly: true);

            if (!pipeServer.StartAndWait(ListenerReadyTimeout))
            {
                pipeServer.Dispose();
                return false;
            }

            _routedPipeServers.Add(pipeName, new RoutedPipeRegistration(pipeServer, attestationProvider));
            return true;
        }
    }

    public void UpdateRoutedPipeAttestation(string pipeName, string attestationJson)
    {
        lock (_routedPipeLock)
        {
            if (_routedPipeServers.TryGetValue(pipeName, out RoutedPipeRegistration? registration))
            {
                registration.AttestationProvider.Update(attestationJson);
            }
        }
    }

    public void StopRoutedPipeServer(string pipeName)
    {
        RoutedPipeRegistration? registration = null;
        lock (_routedPipeLock)
        {
            _routedPipeServers.Remove(pipeName, out registration);
        }

        registration?.PipeServer.Dispose();
    }

    public void StopAllRoutedPipeServers()
    {
        List<McpNamedPipeServer> servers;
        lock (_routedPipeLock)
        {
            servers = _routedPipeServers.Values.Select(value => value.PipeServer).ToList();
            _routedPipeServers.Clear();
        }

        StopServersTogether(servers);
    }

    public void Dispose()
    {
        StopAllRoutedPipeServers();

        List<McpNamedPipeServer> boundServers;
        lock (_boundPipeLock)
        {
            boundServers = _boundPipeServers.Values.ToList();
            _boundPipeServers.Clear();
        }

        StopServersTogether(boundServers);

        _pipeServer?.Dispose();
        _pipeServer = null;
    }

    private static void StopServersTogether(IReadOnlyCollection<McpNamedPipeServer> servers)
    {
        if (servers.Count == 0)
        {
            return;
        }

        Task aggregate = Task.WhenAll(servers.Select(server => server.StopAsync()));
        try
        {
            aggregate.Wait(AggregateStopTimeout);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
        }

        foreach (McpNamedPipeServer server in servers)
        {
            server.DisposeAfterStopRequested();
        }
    }

    private static IHost CreateConnectionHost(Stream input, Stream output)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        McpRhinoPlugin.ConfigurePluginServices(builder.Services);

        builder.Services
            .AddMcpServer()
            .AddRhinoTools()
            .AddRhinoResources()
            .WithStreamServerTransport(input, output);

        return builder.Build();
    }

    private sealed record RoutedPipeRegistration(
        McpNamedPipeServer PipeServer,
        RoutedAttestationProvider AttestationProvider);
}
