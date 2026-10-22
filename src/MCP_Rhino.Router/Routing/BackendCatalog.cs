using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.Logging;

namespace MCP_Rhino.Router.Routing;

// Connections follow use, not Router lifetime. Probe connections (surface discovery and attestation)
// close at once. Work connections are opened only for documents this Router session actually
// targets and stay cached until the document closes, its generation changes, or the Router exits,
// because per-connection backend state (script submissions, Grasshopper sessions) lives on them.
public sealed class BackendCatalog : IAsyncDisposable
{
    private static readonly TimeSpan FailedSurfaceProbeBackoff = TimeSpan.FromSeconds(5);

    private readonly RouteDiscovery _discovery;
    private readonly ILoggerFactory _loggerFactory;
    private readonly object _lock = new();
    private readonly Dictionary<string, WorkEntry> _work = new(StringComparer.Ordinal);
    private readonly HashSet<WorkEntry> _retired = new();
    private readonly Dictionary<string, SurfaceEntry> _surfaces = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private int _ownedConnections;
    private bool _disposed;

    public BackendCatalog(RouteDiscovery discovery, ILoggerFactory loggerFactory)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    // Open work connections, including retired ones still draining in-flight calls.
    public int OwnedConnectionCount => Volatile.Read(ref _ownedConnections);

    public RouteDiscoverySnapshot Discover() => _discovery.Discover();

    public async Task<RouterCatalogSnapshot> RefreshAsync(
        CancellationToken cancellationToken,
        bool retryFailedSurfaceProbes = true)
    {
        RouteDiscoverySnapshot discovery = _discovery.Discover();
        IReadOnlyList<RouteEndpointDescriptor> routable = discovery.RoutableDescriptors;
        RetireStaleWork(discovery);

        IGrouping<string, RouteEndpointDescriptor>[] instances = routable
            .GroupBy(InstanceKey, StringComparer.Ordinal)
            .ToArray();
        lock (_lock)
        {
            HashSet<string> liveKeys = instances.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            foreach (string key in _surfaces.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
            {
                _surfaces.Remove(key);
            }
        }

        SurfaceProbeResult[] probes = await Task.WhenAll(instances.Select(group =>
            EnsureSurfaceAsync(group.Key, group.ToArray(), retryFailedSurfaceProbes, cancellationToken))).ConfigureAwait(false);

        var routes = new List<DocumentRoute>();
        for (int index = 0; index < instances.Length; index++)
        {
            SurfaceProbeResult probe = probes[index];
            foreach (RouteEndpointDescriptor descriptor in instances[index])
            {
                string? diagnostic = null;
                if (probe.Surface is null)
                {
                    diagnostic = probe.Diagnostics.TryGetValue(descriptor.DocumentSessionId, out string? own)
                        ? own
                        : probe.Diagnostics.Values.FirstOrDefault() ?? "SURFACE_UNAVAILABLE";
                }

                routes.Add(new DocumentRoute(descriptor, probe.Surface, diagnostic));
            }
        }

        return new RouterCatalogSnapshot(discovery, routes, SelectCanonicalSurface(routes));
    }

    public async Task<BackendLease> AcquireAsync(
        RouteEndpointDescriptor descriptor,
        BackendAccess access,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        string generationKey = GenerationKey(descriptor);
        WorkEntry? entry;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _work.TryGetValue(descriptor.DocumentSessionId, out WorkEntry? existing);
            if (existing is not null && !string.Equals(existing.GenerationKey, generationKey, StringComparison.Ordinal))
            {
                _work.Remove(descriptor.DocumentSessionId);
                RetireLocked(existing);
                existing = null;
            }

            if (existing is null && access == BackendAccess.Work)
            {
                // Single flight: the connect starts only after this entry won the cache slot.
                WorkEntry created = new(descriptor, generationKey);
                _work.Add(descriptor.DocumentSessionId, created);
                created.Start(Task.Run(() => ConnectWorkAsync(created)));
                _ = created.Connection.ContinueWith(
                    _ => OnWorkConnectCompleted(created),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                existing = created;
            }

            // A probe reuses an established work connection but never waits for or creates one.
            entry = existing is not null && (access == BackendAccess.Work || existing.IsConnected)
                ? existing
                : null;
            if (entry is not null)
            {
                entry.Leases++;
            }
        }

        if (entry is null)
        {
            BackendConnection probe = await BackendConnection.ConnectAsync(
                descriptor,
                _loggerFactory,
                cancellationToken,
                includeSurface: false).ConfigureAwait(false);
            return new BackendLease(probe, this, null);
        }

        try
        {
            BackendConnection connection = await entry.Connection.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new BackendLease(connection, this, entry);
        }
        catch
        {
            ReleaseLease(entry);
            throw;
        }
    }

    // Drops a work connection that failed mid-call so the next call reconnects.
    public void Invalidate(BackendLease lease)
    {
        if (lease.Entry is not { } entry)
        {
            return;
        }

        lock (_lock)
        {
            RemoveIfCurrentLocked(entry);
            RetireLocked(entry);
        }
    }

    // A work connection reported a surface that differs from the cached one for its plug-in instance.
    public void UpdateSurface(RouteEndpointDescriptor descriptor, BackendSurface surface)
    {
        lock (_lock)
        {
            _surfaces[InstanceKey(descriptor)] = SurfaceEntry.Completed(SurfaceProbeResult.Success(surface));
        }
    }

    public async ValueTask DisposeAsync()
    {
        WorkEntry[] entries;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            entries = _work.Values.Concat(_retired).Distinct().ToArray();
            _work.Clear();
            _retired.Clear();
        }

        _lifetime.Cancel();
        foreach (WorkEntry entry in entries)
        {
            await DisposeEntryAsync(entry).ConfigureAwait(false);
        }
    }

    internal void ReleaseLease(WorkEntry entry)
    {
        bool dispose;
        lock (_lock)
        {
            entry.Leases--;
            dispose = ShouldDisposeLocked(entry);
        }

        if (dispose)
        {
            _ = DisposeEntryAsync(entry);
        }
    }

    private async Task<BackendConnection> ConnectWorkAsync(WorkEntry entry)
    {
        try
        {
            BackendConnection connection = await BackendConnection.ConnectAsync(
                entry.Descriptor,
                _loggerFactory,
                _lifetime.Token).ConfigureAwait(false);
            Interlocked.Increment(ref _ownedConnections);
            return connection;
        }
        catch
        {
            lock (_lock)
            {
                RemoveIfCurrentLocked(entry);
                _retired.Remove(entry);
            }

            throw;
        }
    }

    private void OnWorkConnectCompleted(WorkEntry entry)
    {
        bool dispose;
        lock (_lock)
        {
            dispose = ShouldDisposeLocked(entry);
        }

        if (dispose)
        {
            _ = DisposeEntryAsync(entry);
        }
    }

    private async Task<SurfaceProbeResult> EnsureSurfaceAsync(
        string instanceKey,
        IReadOnlyList<RouteEndpointDescriptor> descriptors,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        SurfaceEntry entry;
        lock (_lock)
        {
            if (!_surfaces.TryGetValue(instanceKey, out SurfaceEntry? existing)
                || (existing.IsFailed && (retryFailed || DateTimeOffset.UtcNow - existing.CompletedUtc >= FailedSurfaceProbeBackoff)))
            {
                BackendSurface? known = _work.Values
                    .Where(work => string.Equals(InstanceKey(work.Descriptor), instanceKey, StringComparison.Ordinal))
                    .Select(work => work.ConnectedSurface)
                    .FirstOrDefault(surface => surface is not null);
                existing = known is not null
                    ? SurfaceEntry.Completed(SurfaceProbeResult.Success(known))
                    : SurfaceEntry.Pending(ProbeSurfaceAsync(descriptors));
                _surfaces[instanceKey] = existing;
            }

            entry = existing;
        }

        return await entry.Result.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SurfaceProbeResult> ProbeSurfaceAsync(IReadOnlyList<RouteEndpointDescriptor> descriptors)
    {
        var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (RouteEndpointDescriptor descriptor in descriptors.OrderBy(item => item.DocumentSessionId, StringComparer.Ordinal))
        {
            try
            {
                await using BackendConnection probe = await BackendConnection.ConnectAsync(
                    descriptor,
                    _loggerFactory,
                    _lifetime.Token).ConfigureAwait(false);
                return SurfaceProbeResult.Success(probe.Surface!);
            }
            catch (Exception ex)
            {
                diagnostics[descriptor.DocumentSessionId] = Diagnose(ex);
            }
        }

        return SurfaceProbeResult.Failure(diagnostics);
    }

    private void RetireStaleWork(RouteDiscoverySnapshot discovery)
    {
        Dictionary<string, RouteEndpointDescriptor> routable = discovery.RoutableDescriptors
            .ToDictionary(descriptor => descriptor.DocumentSessionId, StringComparer.Ordinal);
        HashSet<string> unreadableFiles = discovery.Documents
            .Where(status => status.Descriptor is null
                || string.Equals(status.Status, "OWNER_PROCESS_UNVERIFIED", StringComparison.Ordinal))
            .Select(status => Path.GetFileName(status.DescriptorPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            foreach (WorkEntry entry in _work.Values.ToArray())
            {
                bool stale = routable.TryGetValue(entry.Descriptor.DocumentSessionId, out RouteEndpointDescriptor? current)
                    ? !string.Equals(GenerationKey(current), entry.GenerationKey, StringComparison.Ordinal)
                    // A transiently unreadable descriptor is not proof the document went away.
                    : !unreadableFiles.Contains(DescriptorFileName(entry.Descriptor));
                if (stale)
                {
                    _work.Remove(entry.Descriptor.DocumentSessionId);
                    RetireLocked(entry);
                }
            }
        }
    }

    private void RemoveIfCurrentLocked(WorkEntry entry)
    {
        if (_work.TryGetValue(entry.Descriptor.DocumentSessionId, out WorkEntry? current)
            && ReferenceEquals(current, entry))
        {
            _work.Remove(entry.Descriptor.DocumentSessionId);
        }
    }

    private void RetireLocked(WorkEntry entry)
    {
        if (entry.Retired)
        {
            return;
        }

        entry.Retired = true;
        _retired.Add(entry);
        if (ShouldDisposeLocked(entry))
        {
            // Never dispose a client while holding the catalog lock.
            _ = Task.Run(() => DisposeEntryAsync(entry));
        }
    }

    private static bool ShouldDisposeLocked(WorkEntry entry)
    {
        return entry.Retired && entry.Leases == 0 && entry.IsConnected && !entry.DisposeStarted;
    }

    private async Task DisposeEntryAsync(WorkEntry entry)
    {
        lock (_lock)
        {
            if (entry.DisposeStarted)
            {
                return;
            }

            entry.DisposeStarted = true;
            _retired.Remove(entry);
        }

        try
        {
            BackendConnection connection = await entry.Connection.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            Interlocked.Decrement(ref _ownedConnections);
        }
        catch
        {
        }
    }

    private static BackendSurface? SelectCanonicalSurface(IReadOnlyList<DocumentRoute> routes)
    {
        return routes
            .Where(route => route.Surface is not null)
            .GroupBy(route => route.Surface!.Fingerprint, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Max(route => ParseVersion(route.Descriptor.PluginVersion)))
            .ThenBy(group => group.Min(route => route.Descriptor.DocumentSessionId), StringComparer.Ordinal)
            .FirstOrDefault()?
            .First()
            .Surface;
    }

    private static string InstanceKey(RouteEndpointDescriptor descriptor)
    {
        return $"{descriptor.PluginInstanceGeneration}|{descriptor.PluginVersion}";
    }

    // LastUpdatedUtc is deliberately excluded: a plain Save rewrites it and must not reconnect.
    private static string GenerationKey(RouteEndpointDescriptor descriptor)
    {
        string path;
        try
        {
            path = string.IsNullOrWhiteSpace(descriptor.FilePath)
                ? string.Empty
                : RoutePath.Normalize(descriptor.FilePath).ToUpperInvariant();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            path = descriptor.FilePath!;
        }

        return $"{descriptor.PluginInstanceGeneration}|{descriptor.DocumentLifecycleGeneration}|{descriptor.EndpointNonce}|{path}";
    }

    private static string DescriptorFileName(RouteEndpointDescriptor descriptor)
    {
        return $"{descriptor.ProcessId}_{descriptor.RuntimeSerialNumber}.json";
    }

    private static Version ParseVersion(string version)
    {
        return Version.TryParse(version, out Version? parsed) ? parsed : new Version(0, 0);
    }

    internal static bool IsConnectFailure(Exception ex)
    {
        return ex is IOException
            or TimeoutException
            or UnauthorizedAccessException
            or InvalidDataException
            or System.Text.Json.JsonException
            or NotSupportedException
            or ModelContextProtocol.McpException
            or OperationCanceledException
            or BackendConnectException;
    }

    internal static string Diagnose(Exception ex)
    {
        return ex is BackendConnectException connect
            ? connect.Diagnostic
            : $"CONNECT_FAILED: {ex.Message}";
    }

    internal sealed class WorkEntry
    {
        private Task<BackendConnection>? _connection;

        public WorkEntry(RouteEndpointDescriptor descriptor, string generationKey)
        {
            Descriptor = descriptor;
            GenerationKey = generationKey;
        }

        public RouteEndpointDescriptor Descriptor { get; }

        public string GenerationKey { get; }

        public Task<BackendConnection> Connection => _connection!;

        public bool IsConnected => _connection is { IsCompletedSuccessfully: true };

        public BackendSurface? ConnectedSurface => IsConnected ? _connection!.Result.Surface : null;

        public int Leases { get; set; }

        public bool Retired { get; set; }

        public bool DisposeStarted { get; set; }

        public void Start(Task<BackendConnection> connection)
        {
            _connection = connection;
        }
    }

    private sealed class SurfaceEntry
    {
        private SurfaceEntry(Task<SurfaceProbeResult> result)
        {
            Result = result;
        }

        public Task<SurfaceProbeResult> Result { get; }

        public DateTimeOffset CompletedUtc { get; private set; } = DateTimeOffset.UtcNow;

        public bool IsFailed => Result.IsCompleted && (!Result.IsCompletedSuccessfully || Result.Result.Surface is null);

        public static SurfaceEntry Completed(SurfaceProbeResult result) => new(Task.FromResult(result));

        public static SurfaceEntry Pending(Task<SurfaceProbeResult> result)
        {
            var entry = new SurfaceEntry(result);
            _ = result.ContinueWith(
                _ => entry.CompletedUtc = DateTimeOffset.UtcNow,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return entry;
        }
    }

    private sealed record SurfaceProbeResult(BackendSurface? Surface, IReadOnlyDictionary<string, string> Diagnostics)
    {
        public static SurfaceProbeResult Success(BackendSurface surface) =>
            new(surface, new Dictionary<string, string>(StringComparer.Ordinal));

        public static SurfaceProbeResult Failure(IReadOnlyDictionary<string, string> diagnostics) =>
            new(null, diagnostics);
    }
}

public enum BackendAccess
{
    // Cached for the Router's lifetime; used for selection and proxied calls.
    Work,

    // Attestation-only and closed at once, unless a work connection already exists.
    Probe
}

public sealed class BackendLease : IAsyncDisposable
{
    private readonly BackendCatalog _catalog;
    private int _released;

    internal BackendLease(BackendConnection connection, BackendCatalog catalog, BackendCatalog.WorkEntry? entry)
    {
        Connection = connection;
        _catalog = catalog;
        Entry = entry;
    }

    public BackendConnection Connection { get; }

    internal BackendCatalog.WorkEntry? Entry { get; }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        if (Entry is null)
        {
            await Connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        _catalog.ReleaseLease(Entry);
    }
}

public sealed record DocumentRoute(
    RouteEndpointDescriptor Descriptor,
    BackendSurface? Surface,
    string? Diagnostic);

public sealed record RouterCatalogSnapshot(
    RouteDiscoverySnapshot Discovery,
    IReadOnlyList<DocumentRoute> Routes,
    BackendSurface? CanonicalSurface)
{
    public string? CanonicalFingerprint => CanonicalSurface?.Fingerprint;

    public IReadOnlyList<RouteEndpointDescriptor> CanonicalDescriptors => Routes
        .Where(IsCanonical)
        .Select(route => route.Descriptor)
        .OrderBy(descriptor => descriptor.DocumentSessionId, StringComparer.Ordinal)
        .ToArray();

    public bool IsCanonical(DocumentRoute route) =>
        route.Surface is not null
        && CanonicalSurface is not null
        && string.Equals(route.Surface.Fingerprint, CanonicalSurface.Fingerprint, StringComparison.Ordinal);

    public DocumentRoute? Find(string sessionId) => Routes.FirstOrDefault(route =>
        string.Equals(route.Descriptor.DocumentSessionId, sessionId, StringComparison.Ordinal));
}
