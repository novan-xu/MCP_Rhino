using System.Collections.Concurrent;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.Logging;

namespace MCP_Rhino.Router.Routing;

public sealed class BackendCatalog : IAsyncDisposable
{
    private readonly RouteDiscovery _discovery;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, BackendCacheEntry> _connections = new(StringComparer.Ordinal);

    public BackendCatalog(RouteDiscovery discovery, ILoggerFactory loggerFactory)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<RouterCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        RouteDiscoverySnapshot discovery = _discovery.Discover();
        var results = await Task.WhenAll(discovery.RoutableDescriptors.Select(descriptor =>
            ConnectForSnapshotAsync(descriptor, cancellationToken))).ConfigureAwait(false);

        var healthy = results
            .Where(result => result.Connection is not null)
            .Select(result => result.Connection!)
            .ToArray();

        string[] liveSessionIds = discovery.RoutableDescriptors
            .Select(descriptor => descriptor.DocumentSessionId)
            .ToArray();
        foreach ((string sessionId, BackendCacheEntry cached) in _connections)
        {
            if (!liveSessionIds.Contains(sessionId, StringComparer.Ordinal)
                && _connections.TryRemove(sessionId, out _))
            {
                await DisposeQuietlyAsync(cached.ConnectionTask).ConfigureAwait(false);
            }
        }

        IReadOnlyList<BackendConnection> canonical = SelectCanonicalGroup(healthy);
        return new RouterCatalogSnapshot(discovery, results, healthy, canonical);
    }

    public async Task<BackendConnection?> GetHealthyBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        RouterCatalogSnapshot snapshot = await RefreshAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.HealthyConnections.FirstOrDefault(connection =>
            string.Equals(connection.Descriptor.DocumentSessionId, sessionId, StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        BackendCacheEntry[] entries = _connections.Values.ToArray();
        _connections.Clear();
        foreach (BackendCacheEntry entry in entries)
        {
            await DisposeQuietlyAsync(entry.ConnectionTask).ConfigureAwait(false);
        }
    }

    private async Task<BackendConnectionResult> ConnectForSnapshotAsync(
        RouteEndpointDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        string generationKey = $"{descriptor.PluginInstanceGeneration}:{descriptor.DocumentLifecycleGeneration}:{descriptor.LastUpdatedUtc.UtcTicks}";
        BackendCacheEntry entry = _connections.AddOrUpdate(
            descriptor.DocumentSessionId,
            _ => CreateEntry(descriptor, generationKey, cancellationToken),
            (_, existing) => string.Equals(existing.GenerationKey, generationKey, StringComparison.Ordinal)
                ? existing
                : ReplaceEntry(existing, descriptor, generationKey, cancellationToken));

        try
        {
            BackendConnection connection = await entry.ConnectionTask.ConfigureAwait(false);
            return new BackendConnectionResult(descriptor, connection, null);
        }
        catch (Exception ex) when (ex is IOException
            or TimeoutException
            or UnauthorizedAccessException
            or InvalidDataException
            or System.Text.Json.JsonException
            or NotSupportedException
            or ModelContextProtocol.McpException
            or OperationCanceledException)
        {
            _connections.TryRemove(descriptor.DocumentSessionId, out _);
            return new BackendConnectionResult(descriptor, null, ex.Message);
        }
    }

    private BackendCacheEntry CreateEntry(
        RouteEndpointDescriptor descriptor,
        string generationKey,
        CancellationToken cancellationToken)
    {
        return new BackendCacheEntry(
            generationKey,
            BackendConnection.ConnectAsync(descriptor, _loggerFactory, cancellationToken));
    }

    private BackendCacheEntry ReplaceEntry(
        BackendCacheEntry existing,
        RouteEndpointDescriptor descriptor,
        string generationKey,
        CancellationToken cancellationToken)
    {
        _ = DisposeQuietlyAsync(existing.ConnectionTask);
        return CreateEntry(descriptor, generationKey, cancellationToken);
    }

    private static IReadOnlyList<BackendConnection> SelectCanonicalGroup(IReadOnlyList<BackendConnection> healthy)
    {
        return healthy
            .GroupBy(connection => connection.SurfaceFingerprint, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(connection => ParseVersion(connection.Descriptor.PluginVersion)))
            .ThenBy(group => group.Min(connection => connection.Descriptor.DocumentSessionId), StringComparer.Ordinal)
            .FirstOrDefault()?
            .OrderBy(connection => connection.Descriptor.DocumentSessionId, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<BackendConnection>();
    }

    private static Version ParseVersion(string version)
    {
        return Version.TryParse(version, out Version? parsed) ? parsed : new Version(0, 0);
    }

    private static async Task DisposeQuietlyAsync(Task<BackendConnection> connectionTask)
    {
        try
        {
            BackendConnection connection = await connectionTask.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private sealed record BackendCacheEntry(string GenerationKey, Task<BackendConnection> ConnectionTask);
}

public sealed record BackendConnectionResult(
    RouteEndpointDescriptor Descriptor,
    BackendConnection? Connection,
    string? Diagnostic);

public sealed record RouterCatalogSnapshot(
    RouteDiscoverySnapshot Discovery,
    IReadOnlyList<BackendConnectionResult> ConnectionResults,
    IReadOnlyList<BackendConnection> HealthyConnections,
    IReadOnlyList<BackendConnection> CanonicalConnections)
{
    public BackendConnection? CanonicalBackend => CanonicalConnections.FirstOrDefault();

    public string? CanonicalFingerprint => CanonicalBackend?.SurfaceFingerprint;
}
