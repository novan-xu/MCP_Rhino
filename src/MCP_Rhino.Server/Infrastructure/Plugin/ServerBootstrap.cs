namespace MCP_Rhino.Server.Infrastructure.Plugin;

// Entry point invoked by McpRhinoPlugin via reflection from the isolated
// MCP_Rhino.Server.Runtime assembly. That runtime variant deliberately excludes
// all Rhino PlugIn and Command entry types. Only primitives and JSON strings
// cross the load-context boundary.
public sealed class ServerBootstrap : IDisposable
{
    private static readonly TimeSpan ListenerReadyTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AggregateStopTimeout = TimeSpan.FromSeconds(5);

    private readonly object _routedPipeLock = new();
    private readonly Dictionary<string, RoutedPipeRegistration> _routedPipeServers = new(StringComparer.OrdinalIgnoreCase);

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

    private sealed record RoutedPipeRegistration(
        McpNamedPipeServer PipeServer,
        RoutedAttestationProvider AttestationProvider);
}
