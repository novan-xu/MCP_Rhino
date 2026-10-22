extern alias rhinocommon;

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_Rhino.Router.Routing;
using MCP_Rhino.Server.Infrastructure.Plugin;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

// Drives real Router types against the production McpNamedPipeServer over a private registry, so
// real Routers on this machine never see these endpoints. Only the per-connection MCP host is fake.

string root = Path.Combine(Path.GetTempPath(), "MCP_Rhino", "route-capacity-smoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
try
{
    await Scenarios.CapacityAndBusyAsync(Path.Combine(root, "capacity"), loggerFactory);
    await Scenarios.SaveAndGenerationAsync(Path.Combine(root, "save"), loggerFactory);
    await Scenarios.NoLeakAsync(Path.Combine(root, "leak"), loggerFactory);
    await Scenarios.DisconnectClassificationAsync(Path.Combine(root, "disconnect"), loggerFactory);
    await Scenarios.SurfaceChangeAsync(Path.Combine(root, "surface"), loggerFactory);
    Scenarios.RegistrySharing(Path.Combine(root, "registry"));
    Console.WriteLine("[OK] router-route-capacity-smoke-test");
    return 0;
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch (IOException) { }
}

static class Scenarios
{
    private const int RouteCap = 8;
    private static uint _nextSerial = 9000;

    public static async Task CapacityAndBusyAsync(string registryPath, ILoggerFactory loggerFactory)
    {
        var registry = new RouteRegistry(registryPath);
        await using var endpoint = new TestEndpoint(registry, NextSerial(), RouteCap);
        endpoint.Start();
        endpoint.Publish();

        var idle = new List<RouterHarness>();
        try
        {
            for (int index = 0; index < 12; index++)
            {
                idle.Add(new RouterHarness(registry, loggerFactory));
            }

            ListToolsResult[] lists = await Task.WhenAll(idle.Select(router => router.Runtime.ListToolsCoreAsync(CancellationToken.None).AsTask()));
            Require(lists.All(list => list.Tools.Any(tool => tool.Name == "read_echo")), "12 Routers list the live tool surface.");
            idle.ForEach(router => router.StartMonitor());
            await Task.Delay(TimeSpan.FromSeconds(2.5));
            Require(endpoint.Active == 0, $"12 idle Routers with monitors hold 0 route connections (active {endpoint.Active}).");

            await using var fresh = new RouterHarness(registry, loggerFactory);
            var stopwatch = Stopwatch.StartNew();
            CallToolResult selected = await fresh.SelectAsync(endpoint.SessionId);
            Require(selected.IsError != true, $"A 13th Router selects the document on its first attempt ({stopwatch.ElapsedMilliseconds} ms).");
            await WaitUntilAsync(() => endpoint.Active == 1, "server active settles at 1");
            Require(endpoint.Active == fresh.Catalog.OwnedConnectionCount + idle.Sum(router => router.Catalog.OwnedConnectionCount),
                "Server active connections equal Router-owned work connections.");

            // Fill the remaining slots with raw clients that never speak MCP.
            var holders = new List<NamedPipeClientStream>();
            try
            {
                for (int index = 1; index < RouteCap; index++)
                {
                    var holder = new NamedPipeClientStream(".", endpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await holder.ConnectAsync(3000);
                    holders.Add(holder);
                }

                await WaitUntilAsync(() => endpoint.Active == RouteCap, "route fills");
                int acceptedBefore = endpoint.Accepted;
                await using var busyRouter = new RouterHarness(registry, loggerFactory);
                stopwatch.Restart();
                CallToolResult busy = await busyRouter.SelectAsync(endpoint.SessionId);
                long busyMs = stopwatch.ElapsedMilliseconds;
                Require(ErrorCode(busy) == "DOCUMENT_ENDPOINT_BUSY" && Bool(busy, "retryable"),
                    $"A full route returns retryable DOCUMENT_ENDPOINT_BUSY ({ErrorCode(busy)}).");
                Require(busyMs <= 3500, $"The busy result arrives within 3.5 s ({busyMs} ms).");
                Require(Property(busy, "diagnostic") == BackendConnectException.EndpointBusy, "The busy result carries the ENDPOINT_BUSY diagnostic.");

                RouteEndpointDescriptor missing = TestEndpoint.CreateDescriptor(NextSerial(), @"C:\fixtures\missing.3dm");
                registry.Write(missing);
                CallToolResult notListening = await busyRouter.SelectAsync(missing.DocumentSessionId);
                Require(ErrorCode(notListening) == "DOCUMENT_ENDPOINT_UNAVAILABLE"
                        && Property(notListening, "diagnostic") == BackendConnectException.EndpointNotListening,
                    "A missing pipe returns DOCUMENT_ENDPOINT_UNAVAILABLE with ENDPOINT_NOT_LISTENING.");

                string documents = (await busyRouter.CallAsync(RouterRuntime.ListDocumentsToolName, new { })).StructuredContent?.GetRawText() ?? string.Empty;
                Require(documents.Contains(BackendConnectException.EndpointBusy, StringComparison.Ordinal), "list_documents reports ENDPOINT_BUSY.");
                Require(documents.Contains(BackendConnectException.EndpointNotListening, StringComparison.Ordinal), "list_documents reports ENDPOINT_NOT_LISTENING.");
                Require(endpoint.Accepted == acceptedBefore, "The pipe existence check consumes no route instance.");
                registry.DeleteIfGenerationMatches(missing);

                holders[0].Dispose();
                holders.RemoveAt(0);
                await WaitUntilAsync(() => endpoint.Active == RouteCap - 1, "one slot frees");
                Require((await busyRouter.SelectAsync(endpoint.SessionId)).IsError != true, "Selection succeeds once a slot frees.");
            }
            finally
            {
                holders.ForEach(holder => holder.Dispose());
            }
        }
        finally
        {
            foreach (RouterHarness router in idle)
            {
                await router.DisposeAsync();
            }
        }

        await WaitUntilAsync(() => endpoint.Active == 0, "all connections close");
        Require(endpoint.Active == 0, "No route connection outlives its Router.");
    }

    public static async Task SaveAndGenerationAsync(string registryPath, ILoggerFactory loggerFactory)
    {
        var registry = new RouteRegistry(registryPath);
        await using var endpoint = new TestEndpoint(registry, NextSerial(), RouteCap);
        endpoint.Start();
        endpoint.Publish();

        await using var router = new RouterHarness(registry, loggerFactory);
        await router.Runtime.ListToolsCoreAsync(CancellationToken.None);
        router.StartMonitor();
        Require((await router.SelectAsync(endpoint.SessionId)).IsError != true, "Router selects the document.");
        string? connectionBefore = Property(await router.CallAsync("read_echo", new { }), "connectionId");
        int acceptedBefore = endpoint.Accepted;
        int connectedLinesBefore = rhinocommon::Rhino.RhinoApp.ConnectedLineCount;

        Task<CallToolResult> mutation = router.CallAsync("slow_mutation", new { delayMs = 4000 });
        for (int save = 0; save < 20; save++)
        {
            endpoint.Publish();
            await router.Catalog.RefreshAsync(CancellationToken.None);
            await Task.Delay(100);
        }

        CallToolResult mutationResult = await mutation;
        string? connectionAfter = Property(await router.CallAsync("read_echo", new { }), "connectionId");
        Require(endpoint.Accepted == acceptedBefore, $"20 Saves cause 0 reconnects ({endpoint.Accepted - acceptedBefore}).");
        Require(rhinocommon::Rhino.RhinoApp.ConnectedLineCount == connectedLinesBefore, "The listener logs no new client connections during Saves.");
        Require(connectionAfter is not null && connectionAfter == connectionBefore, "The per-connection backend identity survives Saves.");
        Require(mutationResult.IsError != true && Property(mutationResult, "connectionId") == connectionBefore,
            "A 4 s mutation in flight across Saves completes OK.");

        // Save As: the path changes, so the Router re-attests while the old call drains.
        Task<CallToolResult> draining = router.CallAsync("slow_mutation", new { delayMs = 3000 });
        await WaitUntilAsync(() => endpoint.InFlight == 1, "mutation dispatched");
        endpoint.SaveAs(@"C:\fixtures\renamed.3dm");
        CallToolResult afterSaveAs = await router.CallAsync("read_echo", new { });
        string? renamedConnection = Property(afterSaveAs, "connectionId");
        Require(afterSaveAs.IsError != true && renamedConnection != connectionBefore, "After Save As, new calls use a new attested connection.");
        Require(endpoint.Active == 2, "The retired connection stays open while its call is in flight.");
        CallToolResult drained = await draining;
        Require(drained.IsError != true && Property(drained, "connectionId") == connectionBefore,
            "The in-flight call on the retired connection completes.");
        await WaitUntilAsync(() => endpoint.Active == 1, "retired connection disposed after draining");
        Require(router.Catalog.OwnedConnectionCount == 1, "The retired connection is disposed once drained.");

        // Listener restart: a new endpoint nonce.
        await endpoint.RestartAsync();
        CallToolResult afterRestart = await router.CallAsync("read_echo", new { });
        Require(afterRestart.IsError != true && Property(afterRestart, "connectionId") != renamedConnection,
            "After a listener restart, the Router re-attests a new connection.");
        await WaitUntilAsync(() => endpoint.Active == 1 && router.Catalog.OwnedConnectionCount == 1, "one connection after restart");
    }

    public static async Task NoLeakAsync(string registryPath, ILoggerFactory loggerFactory)
    {
        var registry = new RouteRegistry(registryPath);
        await using var endpoint = new TestEndpoint(registry, NextSerial(), RouteCap);
        endpoint.Start();
        endpoint.Publish();

        var routers = Enumerable.Range(0, 12).Select(_ => new RouterHarness(registry, loggerFactory)).ToList();
        try
        {
            await Task.WhenAll(routers.Select(router => router.Runtime.ListToolsCoreAsync(CancellationToken.None).AsTask()));
            routers.ForEach(router => router.StartMonitor());

            // Concurrent first calls on each Router must share one single-flight connect.
            CallToolResult[][] firstCalls = await Task.WhenAll(routers.Select(router => Task.WhenAll(
                router.CallAsync("read_echo", new { filePath = endpoint.FilePath }),
                router.CallAsync("read_echo", new { filePath = endpoint.FilePath }),
                router.CallAsync("read_echo", new { filePath = endpoint.FilePath }))));
            int working = firstCalls.Count(calls => calls.All(call => call.IsError != true));
            Require(working == RouteCap, $"Exactly {RouteCap} of 12 contending Routers get work connections ({working}).");
            Require(firstCalls.Where(calls => calls.All(call => call.IsError != true))
                    .All(calls => calls.Select(call => Property(call, "connectionId")).Distinct().Count() == 1),
                "Concurrent first calls on one Router share a single connection.");
            Require(firstCalls.Where(calls => calls.Any(call => call.IsError == true))
                    .SelectMany(calls => calls).All(call => call.IsError != true || ErrorCode(call) == "DOCUMENT_ENDPOINT_BUSY"),
                "Routers without a slot get DOCUMENT_ENDPOINT_BUSY.");
            await CheckpointAsync(endpoint, routers, "after contention");

            // Four rounds of load: concurrent calls, five Saves, then a quiescent checkpoint.
            for (int round = 0; round < 4; round++)
            {
                using var load = new CancellationTokenSource();
                Task[] loops = routers.Select(router => Task.Run(async () =>
                {
                    while (!load.IsCancellationRequested)
                    {
                        await router.CallAsync("read_echo", new { filePath = endpoint.FilePath });
                    }
                })).ToArray();
                for (int save = 0; save < 5; save++)
                {
                    endpoint.Publish();
                    await Task.Delay(150);
                }

                load.Cancel();
                await Task.WhenAll(loops);
                await CheckpointAsync(endpoint, routers, $"after {(round + 1) * 5} Saves");
            }
        }
        finally
        {
            foreach (RouterHarness router in routers)
            {
                await router.DisposeAsync();
            }

            await WaitUntilAsync(() => endpoint.Active == 0, "all Router connections close");
            Require(endpoint.Active == 0, "0 route connections remain after all Routers are disposed.");
        }
    }

    public static async Task DisconnectClassificationAsync(string registryPath, ILoggerFactory loggerFactory)
    {
        var registry = new RouteRegistry(registryPath);
        await using var endpoint = new TestEndpoint(registry, NextSerial(), RouteCap);
        endpoint.Start();
        endpoint.Publish();
        await using var router = new RouterHarness(registry, loggerFactory);
        Require((await router.SelectAsync(endpoint.SessionId)).IsError != true, "Router selects the disconnect endpoint.");

        Task<CallToolResult> mutation = router.CallAsync("slow_mutation", new { delayMs = 4000 });
        await WaitUntilAsync(() => endpoint.InFlight == 1, "mutation dispatched");
        await endpoint.RestartAsync();
        CallToolResult mutationResult = await mutation;
        Require(ErrorCode(mutationResult) == "MUTATION_OUTCOME_UNKNOWN" && Bool(mutationResult, "outcomeUnknown"),
            $"A backend disconnect during a mutation returns MUTATION_OUTCOME_UNKNOWN ({ErrorCode(mutationResult)}).");

        Task<CallToolResult> read = router.CallAsync("slow_read", new { delayMs = 4000 });
        await WaitUntilAsync(() => endpoint.InFlight == 1, "read dispatched");
        await endpoint.RestartAsync();
        CallToolResult readResult = await read;
        Require(ErrorCode(readResult) == "DOCUMENT_ENDPOINT_UNAVAILABLE" && Bool(readResult, "retryable"),
            $"A backend disconnect during a read returns retryable DOCUMENT_ENDPOINT_UNAVAILABLE ({ErrorCode(readResult)}).");

        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        bool cancelled = false;
        try
        {
            await router.Runtime.CallToolCoreAsync(Call("slow_read", new { delayMs = 4000 }), caller.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Require(cancelled, "Real caller cancellation still cancels the call.");
        Require((await router.CallAsync("read_echo", new { })).IsError != true, "The Router recovers with a new connection.");
    }

    public static async Task SurfaceChangeAsync(string registryPath, ILoggerFactory loggerFactory)
    {
        var registry = new RouteRegistry(registryPath);
        await using var first = new TestEndpoint(registry, NextSerial(), RouteCap);
        first.Start();
        first.Publish();
        await using var router = new RouterHarness(registry, loggerFactory);
        await router.Runtime.ListToolsCoreAsync(CancellationToken.None);
        Require(!await router.Runtime.DetectToolSurfaceChangeAsync(CancellationToken.None), "No change is reported for a stable surface.");

        await using var second = new TestEndpoint(registry, NextSerial(), RouteCap, pluginVersion: "2.0.0", surfaceVariant: "v2");
        second.Start();
        second.Publish();
        bool changed = await router.Runtime.DetectToolSurfaceChangeAsync(CancellationToken.None);
        bool changedAgain = await router.Runtime.DetectToolSurfaceChangeAsync(CancellationToken.None);
        Require(changed && !changedAgain, "A new plug-in instance causes exactly one tools/list_changed.");
        Require(second.Accepted == 1, $"The new instance is probed once ({second.Accepted}).");
        await WaitUntilAsync(() => first.Active == 0 && second.Active == 0, "probes close");
        Require(router.Catalog.OwnedConnectionCount == 0, "Surface discovery leaves no connection held.");
        ListToolsResult tools = await router.Runtime.ListToolsCoreAsync(CancellationToken.None);
        Require(tools.Tools.Any(tool => tool.Description?.Contains("v2", StringComparison.Ordinal) == true), "The newer canonical surface is published.");
    }

    public static void RegistrySharing(string registryPath)
    {
        var registry = new RouteRegistry(registryPath);
        RouteEndpointDescriptor descriptor = TestEndpoint.CreateDescriptor(NextSerial(), @"C:\fixtures\shared.3dm");
        registry.Write(descriptor);
        using var stop = new CancellationTokenSource();
        int reads = 0;
        Task reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                registry.ReadAll();
                reads++;
            }
        });

        int failures = 0;
        for (int write = 0; write < 1000; write++)
        {
            try
            {
                registry.Write(descriptor with { LastUpdatedUtc = DateTimeOffset.UtcNow });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures++;
            }
        }

        stop.Cancel();
        reader.Wait();
        Require(failures == 0, $"1,000 descriptor replaces under a looping reader cause 0 write failures ({failures} failures, {reads} reads).");

        // A retry alone cannot pass this: the reader holds the descriptor open for the whole replace.
        string path = registry.GetDescriptorPath(descriptor.ProcessId, descriptor.RuntimeSerialNumber);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            registry.Write(descriptor with { DisplayName = "replaced.3dm" });
            using var reader2 = new StreamReader(held);
            Require(reader2.ReadToEnd().Contains("shared.3dm", StringComparison.Ordinal),
                "A reader holding the descriptor open keeps reading the old content.");
        }

        Require(registry.ReadAll().Single().Descriptor?.DisplayName == "replaced.3dm",
            "The descriptor is replaced while a delete-sharing reader holds it open.");
    }

    private static async Task CheckpointAsync(TestEndpoint endpoint, IReadOnlyList<RouterHarness> routers, string label)
    {
        await WaitUntilAsync(() => endpoint.Active == routers.Sum(router => router.Catalog.OwnedConnectionCount), label);
        int owned = routers.Sum(router => router.Catalog.OwnedConnectionCount);
        Require(endpoint.Active == owned, $"Checkpoint {label}: server active {endpoint.Active} equals Router-owned {owned}.");
    }

    private static uint NextSerial() => Interlocked.Increment(ref _nextSerial);

    public static async Task WaitUntilAsync(Func<bool> condition, string label, double seconds = 10)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(seconds))
            {
                Console.WriteLine($"[WARN] Timed out waiting for: {label}");
                return;
            }

            await Task.Delay(25);
        }
    }

    public static CallToolRequestParams Call(string name, object arguments)
    {
        Dictionary<string, JsonElement>? values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(arguments));
        return new CallToolRequestParams { Name = name, Arguments = values };
    }

    public static string? Property(CallToolResult result, string name)
    {
        return result.StructuredContent is JsonElement content
            && content.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool Bool(CallToolResult result, string name)
    {
        return result.StructuredContent is JsonElement content
            && content.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static string? ErrorCode(CallToolResult result) => Property(result, "code");

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
        Console.WriteLine("[OK] " + message);
    }
}

sealed class RouterHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _monitor = new();
    private Task? _monitorLoop;

    public RouterHarness(RouteRegistry registry, ILoggerFactory loggerFactory)
    {
        Catalog = new BackendCatalog(new RouteDiscovery(registry), loggerFactory);
        Runtime = new RouterRuntime(Catalog);
    }

    public BackendCatalog Catalog { get; }

    public RouterRuntime Runtime { get; }

    // Same cadence and call as RouterCatalogMonitor, minus sending the notification.
    public void StartMonitor()
    {
        _monitorLoop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(_monitor.Token))
                {
                    await Runtime.DetectToolSurfaceChangeAsync(_monitor.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    public Task<CallToolResult> SelectAsync(string sessionId) =>
        CallAsync(RouterRuntime.SelectDocumentToolName, new { sessionId });

    public async Task<CallToolResult> CallAsync(string name, object arguments) =>
        await Runtime.CallToolCoreAsync(Scenarios.Call(name, arguments), CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        _monitor.Cancel();
        if (_monitorLoop is not null)
        {
            await _monitorLoop;
        }

        await Catalog.DisposeAsync();
        _monitor.Dispose();
    }
}

sealed class TestEndpoint : IAsyncDisposable
{
    private static readonly JsonElement EmptySchema = Schema("""{"type":"object","properties":{},"additionalProperties":false}""");
    private static readonly JsonElement DelaySchema = Schema("""{"type":"object","properties":{"delayMs":{"type":"integer"}},"additionalProperties":false}""");
    private static readonly JsonElement PathSchema = Schema("""{"type":"object","properties":{"filePath":{"type":"string"}},"additionalProperties":false}""");

    private readonly RouteRegistry _registry;
    private readonly int _cap;
    private readonly string _surfaceVariant;
    private readonly object _lock = new();
    private RouteEndpointDescriptor _descriptor;
    private McpNamedPipeServer? _server;
    private int _accepted;
    private int _active;
    private int _inFlight;

    public TestEndpoint(RouteRegistry registry, uint serial, int cap, string pluginVersion = "1.0.0", string surfaceVariant = "v1")
    {
        _registry = registry;
        _cap = cap;
        _surfaceVariant = surfaceVariant;
        _descriptor = CreateDescriptor(serial, $@"C:\fixtures\doc{serial}.3dm") with { PluginVersion = pluginVersion };
    }

    public string SessionId => Current.DocumentSessionId;

    public string PipeName => Current.PipeName;

    public string FilePath => Current.FilePath!;

    public int Accepted => Volatile.Read(ref _accepted);

    public int Active => Volatile.Read(ref _active);

    public int InFlight => Volatile.Read(ref _inFlight);

    private RouteEndpointDescriptor Current
    {
        get { lock (_lock) { return _descriptor; } }
    }

    public static RouteEndpointDescriptor CreateDescriptor(uint serial, string path)
    {
        using Process current = Process.GetCurrentProcess();
        return new RouteEndpointDescriptor
        {
            DocumentSessionId = Guid.NewGuid().ToString("N"),
            PluginInstanceGeneration = Guid.NewGuid().ToString("N"),
            DocumentLifecycleGeneration = 1,
            EndpointNonce = Guid.NewGuid().ToString("N"),
            ProcessId = current.Id,
            ProcessStartTimeUtcTicks = current.StartTime.ToUniversalTime().Ticks,
            RuntimeSerialNumber = serial,
            PipeName = RouteProtocol.ForDocument(current.Id, serial),
            FilePath = path,
            DisplayName = Path.GetFileName(path),
            Routable = true,
            PluginVersion = "1.0.0",
            Status = "READY"
        };
    }

    public void Start()
    {
        _server = new McpNamedPipeServer(
            PipeName,
            CreateHost,
            stopOnPipeCreateFailure: true,
            maxConcurrentConnections: _cap,
            currentUserOnly: true);
        if (!_server.StartAndWait(TimeSpan.FromSeconds(3)))
        {
            throw new InvalidOperationException("Test route listener did not start.");
        }
    }

    // A plain Save: the plug-in rewrites the descriptor with only a new LastUpdatedUtc.
    public void Publish()
    {
        _registry.Write(Current with { LastUpdatedUtc = DateTimeOffset.UtcNow });
    }

    public void SaveAs(string path)
    {
        lock (_lock)
        {
            _descriptor = _descriptor with { FilePath = path, DisplayName = Path.GetFileName(path) };
        }

        Publish();
    }

    public async Task RestartAsync()
    {
        McpNamedPipeServer? server = _server;
        if (server is not null)
        {
            await server.StopAsync();
            server.Dispose();
        }

        lock (_lock)
        {
            _descriptor = _descriptor with { EndpointNonce = Guid.NewGuid().ToString("N") };
        }

        Start();
        Publish();
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }

        _registry.DeleteIfGenerationMatches(Current);
    }

    private IHost CreateHost(Stream input, Stream output)
    {
        Interlocked.Increment(ref _accepted);
        Interlocked.Increment(ref _active);
        string connectionId = Guid.NewGuid().ToString("N");
        string description = $"Routed test tool ({_surfaceVariant}).";

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithListToolsHandler((request, cancellationToken) => ValueTask.FromResult(new ListToolsResult
            {
                Tools = new List<Tool>
                {
                    new() { Name = "read_echo", Description = description, InputSchema = PathSchema, Annotations = Annotations(readOnly: true) },
                    new() { Name = "slow_read", Description = description, InputSchema = DelaySchema, Annotations = Annotations(readOnly: true) },
                    new() { Name = "slow_mutation", Description = description, InputSchema = DelaySchema, Annotations = Annotations(readOnly: false) }
                }
            }))
            .WithCallToolHandler(async (request, cancellationToken) =>
            {
                Interlocked.Increment(ref _inFlight);
                try
                {
                    if (request.Params?.Arguments is { } arguments
                        && arguments.TryGetValue("delayMs", out JsonElement delay))
                    {
                        await Task.Delay(delay.GetInt32(), cancellationToken);
                    }

                    object payload = new { connectionId, tool = request.Params?.Name };
                    return new CallToolResult
                    {
                        IsError = false,
                        Content = new List<ContentBlock> { new TextContentBlock { Text = JsonSerializer.Serialize(payload) } },
                        StructuredContent = JsonSerializer.SerializeToElement(payload)
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            })
            .WithListResourcesHandler((request, cancellationToken) => ValueTask.FromResult(new ListResourcesResult { Resources = new List<Resource>() }))
            .WithListResourceTemplatesHandler((request, cancellationToken) => ValueTask.FromResult(new ListResourceTemplatesResult { ResourceTemplates = new List<ResourceTemplate>() }))
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                if (context.JsonRpcMessage is JsonRpcRequest rpcRequest
                    && string.Equals(rpcRequest.Method, RouteProtocol.AttestationRequestMethod, StringComparison.Ordinal))
                {
                    // Read at request time, like the plug-in's updatable attestation provider.
                    JsonNode? result = JsonSerializer.SerializeToNode(Attestation(Current));
                    await context.Server.SendMessageAsync(new JsonRpcResponse { Id = rpcRequest.Id, Result = result }, cancellationToken);
                    return;
                }

                await next(context, cancellationToken);
            }))
            .WithStreamServerTransport(input, output);
        return new CountingHost(builder.Build(), () => Interlocked.Decrement(ref _active));
    }

    private static RouteEndpointAttestation Attestation(RouteEndpointDescriptor descriptor) => new()
    {
        DocumentSessionId = descriptor.DocumentSessionId,
        PluginInstanceGeneration = descriptor.PluginInstanceGeneration,
        DocumentLifecycleGeneration = descriptor.DocumentLifecycleGeneration,
        EndpointNonce = descriptor.EndpointNonce,
        ProcessId = descriptor.ProcessId,
        ProcessStartTimeUtcTicks = descriptor.ProcessStartTimeUtcTicks,
        RuntimeSerialNumber = descriptor.RuntimeSerialNumber,
        PipeName = descriptor.PipeName,
        FilePath = descriptor.FilePath!,
        ServerVersion = descriptor.PluginVersion
    };

    private static ToolAnnotations Annotations(bool readOnly) => new()
    {
        ReadOnlyHint = readOnly,
        DestructiveHint = false,
        OpenWorldHint = false
    };

    private static JsonElement Schema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

// Counts live per-connection hosts; McpNamedPipeServer disposes each host when its client leaves.
sealed class CountingHost : IHost
{
    private readonly IHost _inner;
    private readonly Action _onDisposed;
    private int _disposed;

    public CountingHost(IHost inner, Action onDisposed)
    {
        _inner = inner;
        _onDisposed = onDisposed;
    }

    public IServiceProvider Services => _inner.Services;

    public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _inner.StopAsync(cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _inner.Dispose();
            _onDisposed();
        }
    }
}
