using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP_Rhino.Router.Routing;
using MCP_Rhino.Transport.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

string testRoot = Path.Combine(Path.GetTempPath(), "MCP_Rhino", "router-smoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
try
{
    await RunAsync(testRoot);
    Console.WriteLine("[OK] multi-document-rhino-router-smoke-test");
    return 0;
}
finally
{
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

static async Task RunAsync(string registryPath)
{
    using Process current = Process.GetCurrentProcess();
    int processId = current.Id;
    long processStartTicks = current.StartTime.ToUniversalTime().Ticks;
    var registry = new RouteRegistry(registryPath);

    RouteEndpointDescriptor descriptorA = Descriptor(processId, processStartTicks, 101, @"C:\fixtures\A.3dm", "A.3dm");
    RouteEndpointDescriptor descriptorB = Descriptor(processId, processStartTicks, 102, @"C:\fixtures\B.3dm", "B.3dm");
    RouteEndpointDescriptor descriptorC = Descriptor(processId, processStartTicks, 103, @"C:\fixtures\A.3dm", "A duplicate.3dm");
    RouteEndpointDescriptor attestedDescriptor = Descriptor(processId, processStartTicks, 104, @"C:\fixtures\forged.3dm", "forged.3dm");
    RouteEndpointDescriptor forgedDescriptor = attestedDescriptor with { DocumentSessionId = Guid.NewGuid().ToString("N") };

    await using var endpointA = new FakeRouteEndpoint(descriptorA);
    await using var endpointB = new FakeRouteEndpoint(descriptorB);
    await using var endpointC = new FakeRouteEndpoint(descriptorC);
    await using var forgedEndpoint = new FakeRouteEndpoint(attestedDescriptor);
    await Task.WhenAll(endpointA.StartAsync(), endpointB.StartAsync(), endpointC.StartAsync(), forgedEndpoint.StartAsync());

    registry.Write(descriptorA);
    registry.Write(descriptorB);
    registry.Write(forgedDescriptor);
    RouteEndpointDescriptor stale = Descriptor(processId, processStartTicks + TimeSpan.TicksPerSecond, 105, @"C:\fixtures\stale.3dm", "stale.3dm");
    registry.Write(stale);
    File.WriteAllText(Path.Combine(registryPath, "corrupt.json"), "{not-json");

    using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
    await using var catalogOne = new BackendCatalog(new RouteDiscovery(registry), loggerFactory);
    await using var catalogTwo = new BackendCatalog(new RouteDiscovery(registry), loggerFactory);
    var routerOne = new RouterRuntime(catalogOne);
    var routerTwo = new RouterRuntime(catalogTwo);

    ListToolsResult tools = await routerOne.ListToolsCoreAsync(CancellationToken.None);
    Require(tools.Tools.Count == 5, "Router exposes three controls plus both paginated backend tools.");
    Require(tools.Tools.Any(tool => tool.Name == "echo_document"), "Canonical backend tool is proxied.");
    Require(tools.Tools.Any(tool => tool.Name == "selected_only_echo"), "Second paginated backend tool is proxied.");

    RouterCatalogSnapshot snapshot = await catalogOne.RefreshAsync(CancellationToken.None);
    Require(snapshot.CanonicalBackend?.Resources.Count == 2, "Paginated resources are fully collected.");
    Require(snapshot.CanonicalBackend?.ResourceTemplates.Count == 2, "Paginated resource templates are fully collected.");
    Require(!File.Exists(registry.GetDescriptorPath(stale.ProcessId, stale.RuntimeSerialNumber)), "Proven-stale descriptor is cleaned.");

    CallToolResult byPath = await routerOne.CallToolCoreAsync(Call("echo_document", new { filePath = descriptorA.FilePath, value = "path" }), CancellationToken.None);
    Require(byPath.IsError != true && Property(byPath, "documentSessionId") == descriptorA.DocumentSessionId, "Unique filePath routes to the matching endpoint.");

    CallToolResult noSelection = await routerTwo.CallToolCoreAsync(Call("selected_only_echo", new { value = "none" }), CancellationToken.None);
    Require(ErrorCode(noSelection) == "NO_DOCUMENT_SELECTED", "A tool without filePath requires explicit selection.");

    Require((await routerOne.CallToolCoreAsync(Call(RouterRuntime.SelectDocumentToolName, new { sessionId = descriptorA.DocumentSessionId }), CancellationToken.None)).IsError != true, "First Router selects document A.");
    Require((await routerTwo.CallToolCoreAsync(Call(RouterRuntime.SelectDocumentToolName, new { sessionId = descriptorB.DocumentSessionId }), CancellationToken.None)).IsError != true, "Second Router independently selects document B.");

    CallToolResult firstSelected = await routerOne.CallToolCoreAsync(Call("selected_only_echo", new { value = "one" }), CancellationToken.None);
    CallToolResult secondSelected = await routerTwo.CallToolCoreAsync(Call("selected_only_echo", new { value = "two" }), CancellationToken.None);
    Require(Property(firstSelected, "documentSessionId") == descriptorA.DocumentSessionId, "First Router retains its own selection.");
    Require(Property(secondSelected, "documentSessionId") == descriptorB.DocumentSessionId, "Second Router retains a different selection.");

    int callsBeforeConflict = endpointA.BusinessCallCount;
    CallToolResult conflict = await routerOne.CallToolCoreAsync(Call("echo_document", new { filePath = descriptorB.FilePath, value = "wrong" }), CancellationToken.None);
    Require(ErrorCode(conflict) == "DOCUMENT_TARGET_CONFLICT", "Selected session plus conflicting filePath is rejected.");
    Require(endpointA.BusinessCallCount == callsBeforeConflict, "Target conflict performs no backend call.");

    registry.Write(descriptorC);
    await using var catalogThree = new BackendCatalog(new RouteDiscovery(registry), loggerFactory);
    var routerThree = new RouterRuntime(catalogThree);
    CallToolResult ambiguous = await routerThree.CallToolCoreAsync(Call("echo_document", new { filePath = descriptorA.FilePath, value = "ambiguous" }), CancellationToken.None);
    Require(ErrorCode(ambiguous) == "DOCUMENT_TARGET_AMBIGUOUS", "Duplicate open paths require an explicit session selection.");

    CallToolResult documents = await routerThree.CallToolCoreAsync(Call(RouterRuntime.ListDocumentsToolName, new { }), CancellationToken.None);
    string documentsJson = documents.StructuredContent?.GetRawText() ?? string.Empty;
    Require(documentsJson.Contains("ROUTE_ATTESTATION_MISMATCH", StringComparison.Ordinal), "Forged descriptor is quarantined by private attestation.");
    Require(
        documentsJson.Contains("INVALID_DESCRIPTOR", StringComparison.Ordinal)
            || documentsJson.Contains("READ_FAILED", StringComparison.Ordinal),
        "Corrupt descriptor is reported without failing discovery.");

    Require(endpointA.MaxConcurrentConnections >= 2 && endpointB.MaxConcurrentConnections >= 2, "Multiple Router clients coexist on route endpoints.");
}

static RouteEndpointDescriptor Descriptor(int processId, long processStartTicks, uint serial, string path, string displayName)
{
    return new RouteEndpointDescriptor
    {
        DocumentSessionId = Guid.NewGuid().ToString("N"),
        PluginInstanceGeneration = Guid.NewGuid().ToString("N"),
        DocumentLifecycleGeneration = 1,
        EndpointNonce = Guid.NewGuid().ToString("N"),
        ProcessId = processId,
        ProcessStartTimeUtcTicks = processStartTicks,
        RuntimeSerialNumber = serial,
        PipeName = RouteProtocol.ForDocument(processId, serial),
        FilePath = path,
        DisplayName = displayName,
        Routable = true,
        PluginVersion = "1.0.0",
        Status = "READY"
    };
}

static CallToolRequestParams Call(string name, object arguments)
{
    Dictionary<string, JsonElement>? values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
        JsonSerializer.Serialize(arguments));
    return new CallToolRequestParams { Name = name, Arguments = values };
}

static string? Property(CallToolResult result, string name)
{
    return result.StructuredContent is JsonElement content
        && content.TryGetProperty(name, out JsonElement value)
        ? value.GetString()
        : null;
}

static string? ErrorCode(CallToolResult result) => Property(result, "code");

static void Require(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
    Console.WriteLine("[OK] " + message);
}

sealed class FakeRouteEndpoint : IAsyncDisposable
{
    private static readonly JsonElement EchoSchema = ParseSchema("""
        {"type":"object","properties":{"filePath":{"type":"string"},"value":{"type":"string"}},"required":["value"],"additionalProperties":false}
        """);
    private static readonly JsonElement SelectedSchema = ParseSchema("""
        {"type":"object","properties":{"value":{"type":"string"}},"required":["value"],"additionalProperties":false}
        """);

    private readonly RouteEndpointDescriptor _descriptor;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _tasksLock = new();
    private readonly List<Task> _connections = new();
    private Task? _acceptLoop;
    private int _activeConnections;
    private int _maxConcurrentConnections;
    private int _businessCallCount;

    public FakeRouteEndpoint(RouteEndpointDescriptor descriptor) => _descriptor = descriptor;

    public int BusinessCallCount => Volatile.Read(ref _businessCallCount);
    public int MaxConcurrentConnections => Volatile.Read(ref _maxConcurrentConnections);

    public async Task StartAsync()
    {
        _acceptLoop = AcceptLoopAsync(_shutdown.Token);
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop; } catch (OperationCanceledException) { }
        }

        Task[] connections;
        lock (_tasksLock) { connections = _connections.ToArray(); }
        try { await Task.WhenAll(connections); } catch (OperationCanceledException) { }
        _shutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _descriptor.PipeName,
                PipeDirection.InOut,
                16,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            _ready.TrySetResult();
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch
            {
                await pipe.DisposeAsync();
                throw;
            }

            Task connection = RunConnectionAsync(pipe, cancellationToken);
            lock (_tasksLock) { _connections.Add(connection); }
        }
    }

    private async Task RunConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        int active = Interlocked.Increment(ref _activeConnections);
        UpdateMaximum(active);
        try
        {
            await using (pipe)
            using (IHost host = CreateHost(pipe))
            {
                await host.RunAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }

    private IHost CreateHost(Stream stream)
    {
        RouteEndpointAttestation attestation = new()
        {
            DocumentSessionId = _descriptor.DocumentSessionId,
            PluginInstanceGeneration = _descriptor.PluginInstanceGeneration,
            DocumentLifecycleGeneration = _descriptor.DocumentLifecycleGeneration,
            EndpointNonce = _descriptor.EndpointNonce,
            ProcessId = _descriptor.ProcessId,
            ProcessStartTimeUtcTicks = _descriptor.ProcessStartTimeUtcTicks,
            RuntimeSerialNumber = _descriptor.RuntimeSerialNumber,
            PipeName = _descriptor.PipeName,
            FilePath = _descriptor.FilePath!,
            ServerVersion = _descriptor.PluginVersion
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithListToolsHandler((request, cancellationToken) =>
            {
                bool second = string.Equals(request.Params?.Cursor, "tools-2", StringComparison.Ordinal);
                Tool tool = second
                    ? new Tool { Name = "selected_only_echo", Description = "Echo selected document.", InputSchema = SelectedSchema, Annotations = ReadOnlyAnnotations() }
                    : new Tool { Name = "echo_document", Description = "Echo routed document.", InputSchema = EchoSchema, Annotations = ReadOnlyAnnotations() };
                return ValueTask.FromResult(new ListToolsResult { Tools = new List<Tool> { tool }, NextCursor = second ? null : "tools-2" });
            })
            .WithCallToolHandler((request, cancellationToken) =>
            {
                Interlocked.Increment(ref _businessCallCount);
                object payload = new { documentSessionId = _descriptor.DocumentSessionId, _descriptor.FilePath, tool = request.Params?.Name };
                return ValueTask.FromResult(new CallToolResult
                {
                    IsError = false,
                    Content = new List<ContentBlock> { new TextContentBlock { Text = JsonSerializer.Serialize(payload) } },
                    StructuredContent = JsonSerializer.SerializeToElement(payload)
                });
            })
            .WithListResourcesHandler((request, cancellationToken) =>
            {
                bool second = string.Equals(request.Params?.Cursor, "resources-2", StringComparison.Ordinal);
                var resource = new Resource { Name = second ? "two" : "one", Uri = $"mcp-rhino://fake/{(second ? 2 : 1)}" };
                return ValueTask.FromResult(new ListResourcesResult { Resources = new List<Resource> { resource }, NextCursor = second ? null : "resources-2" });
            })
            .WithListResourceTemplatesHandler((request, cancellationToken) =>
            {
                bool second = string.Equals(request.Params?.Cursor, "templates-2", StringComparison.Ordinal);
                var template = new ResourceTemplate { Name = second ? "two" : "one", UriTemplate = $"mcp-rhino://fake/{(second ? "two" : "one")}/{{id}}" };
                return ValueTask.FromResult(new ListResourceTemplatesResult { ResourceTemplates = new List<ResourceTemplate> { template }, NextCursor = second ? null : "templates-2" });
            })
            .WithReadResourceHandler((request, cancellationToken) => ValueTask.FromResult(new ReadResourceResult { Contents = new List<ResourceContents>() }))
            .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
            {
                if (context.JsonRpcMessage is JsonRpcRequest rpcRequest
                    && string.Equals(rpcRequest.Method, RouteProtocol.AttestationRequestMethod, StringComparison.Ordinal))
                {
                    JsonNode? result = JsonSerializer.SerializeToNode(attestation);
                    await context.Server.SendMessageAsync(new JsonRpcResponse { Id = rpcRequest.Id, Result = result }, cancellationToken);
                    return;
                }

                await next(context, cancellationToken);
            }))
            .WithStreamServerTransport(stream, stream);
        return builder.Build();
    }

    private void UpdateMaximum(int active)
    {
        int current;
        do
        {
            current = Volatile.Read(ref _maxConcurrentConnections);
            if (active <= current) { return; }
        }
        while (Interlocked.CompareExchange(ref _maxConcurrentConnections, active, current) != current);
    }

    private static ToolAnnotations ReadOnlyAnnotations() => new()
    {
        ReadOnlyHint = true,
        DestructiveHint = false,
        OpenWorldHint = false
    };

    private static JsonElement ParseSchema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
