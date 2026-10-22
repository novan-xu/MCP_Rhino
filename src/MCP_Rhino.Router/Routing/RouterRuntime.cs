using System.Text.Json;
using MCP_Rhino.Transport.Routing;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using MCP_Rhino.Offline.Server;

namespace MCP_Rhino.Router.Routing;

public sealed class RouterRuntime
{
    public const string ListDocumentsToolName = "rhino_router_list_documents";
    public const string SelectDocumentToolName = "rhino_router_select_document";
    public const string GetSelectedDocumentToolName = "rhino_router_get_selected_document";
    public const string ReservedToolPrefix = "rhino_router_";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<Tool> ControlTools = CreateControlTools();

    private readonly BackendCatalog _catalog;
    private readonly IReadOnlyDictionary<string, McpServerTool> _offlineTools = OfflineToolRegistration.CreateTools();
    private readonly object _selectionLock = new();
    private readonly object _frontendLock = new();
    private string? _selectedSessionId;
    private McpServer? _frontendServer;
    private string? _publishedSurfaceFingerprint;

    public RouterRuntime(BackendCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public async ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        return await ListToolsCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ListToolsResult> ListToolsCoreAsync(CancellationToken cancellationToken)
    {
        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Tool> backendTools = GetCompatibleTools(snapshot);
        string fingerprint = GetPublishedSurfaceFingerprint(snapshot);
        lock (_frontendLock)
        {
            _publishedSurfaceFingerprint = fingerprint;
        }

        return new ListToolsResult
        {
            Tools = ControlTools.Concat(_offlineTools.Values.Select(tool => tool.ProtocolTool)).Concat(backendTools).ToList()
        };
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        if (request.Params is { } parameters && _offlineTools.TryGetValue(parameters.Name, out McpServerTool? offlineTool))
        {
            CallToolResult? error = ValidateOfflineSource(parameters);
            if (error is not null) return error;
            return await offlineTool.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        }
        return await CallToolCoreAsync(request.Params, cancellationToken).ConfigureAwait(false);
    }

    public CallToolResult? ValidateOfflineSource(CallToolRequestParams parameters)
    {
        // Offline execution must not wait for or depend on live backend handshakes.
        if (TryGetStringArgument(parameters, "filePath", out string? path) &&
            _catalog.Discover().Documents.Any(document => document.Descriptor is { } descriptor && RoutePath.Equals(path, descriptor.FilePath)))
            return RouterErrors.Create("OFFLINE_SOURCE_OPEN", "This source is open in Rhino. Select its live session and use live tools. Do not close, save, or replace the user's document; if offline work on this file is really needed, ask the user.");
        return null;
    }

    public async ValueTask<CallToolResult> CallToolCoreAsync(
        CallToolRequestParams? parameters,
        CancellationToken cancellationToken)
    {
        if (parameters is null || string.IsNullOrWhiteSpace(parameters.Name))
        {
            return RouterErrors.Create("INVALID_TOOL_REQUEST", "A tool name is required.");
        }

        return parameters.Name switch
        {
            ListDocumentsToolName => await ListDocumentsAsync(cancellationToken).ConfigureAwait(false),
            SelectDocumentToolName => await SelectDocumentAsync(parameters, cancellationToken).ConfigureAwait(false),
            GetSelectedDocumentToolName => await GetSelectedDocumentAsync(cancellationToken).ConfigureAwait(false),
            _ => await ProxyToolAsync(parameters, cancellationToken).ConfigureAwait(false)
        };
    }

    public async ValueTask<ListResourcesResult> ListResourcesAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return new ListResourcesResult
        {
            Resources = snapshot.CanonicalSurface?.Resources.ToList() ?? new List<Resource>()
        };
    }

    public async ValueTask<ListResourceTemplatesResult> ListResourceTemplatesAsync(
        RequestContext<ListResourceTemplatesRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        return new ListResourceTemplatesResult
        {
            ResourceTemplates = snapshot.CanonicalSurface?.ResourceTemplates.ToList() ?? new List<ResourceTemplate>()
        };
    }

    public async ValueTask<ReadResourceResult> ReadResourceAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        if (request.Params is null)
        {
            throw new McpProtocolException("A resource URI is required.");
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        string? selectedSessionId = GetSelectedSessionId();
        IEnumerable<RouteEndpointDescriptor> candidates = snapshot.CanonicalDescriptors
            .OrderBy(descriptor => string.Equals(descriptor.DocumentSessionId, selectedSessionId, StringComparison.Ordinal) ? 0 : 1);
        foreach (RouteEndpointDescriptor descriptor in candidates)
        {
            BackendLease lease;
            try
            {
                lease = await _catalog.AcquireAsync(descriptor, BackendAccess.Probe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (BackendCatalog.IsConnectFailure(ex))
            {
                continue;
            }

            await using (lease.ConfigureAwait(false))
            {
                return await lease.Connection.Client.ReadResourceAsync(request.Params, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new McpProtocolException("No compatible saved Rhino document endpoint is available.");
    }

    public async Task CheckForToolSurfaceChangeAsync(CancellationToken cancellationToken)
    {
        McpServer? frontend;
        lock (_frontendLock)
        {
            frontend = _frontendServer;
        }

        if (frontend is null)
        {
            return;
        }

        if (await DetectToolSurfaceChangeAsync(cancellationToken).ConfigureAwait(false))
        {
            await frontend.SendNotificationAsync(
                NotificationMethods.ToolListChangedNotification,
                cancellationToken).ConfigureAwait(false);
        }
    }

    // Reads descriptors only; probes a backend just when a new plug-in instance appears.
    public async Task<bool> DetectToolSurfaceChangeAsync(CancellationToken cancellationToken)
    {
        string? published;
        lock (_frontendLock)
        {
            published = _publishedSurfaceFingerprint;
        }

        if (published is null)
        {
            return false;
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(
            cancellationToken,
            retryFailedSurfaceProbes: false).ConfigureAwait(false);
        string current = GetPublishedSurfaceFingerprint(snapshot);
        if (string.Equals(current, published, StringComparison.Ordinal))
        {
            return false;
        }

        lock (_frontendLock)
        {
            if (!string.Equals(_publishedSurfaceFingerprint, published, StringComparison.Ordinal))
            {
                return false;
            }

            _publishedSurfaceFingerprint = current;
        }

        return true;
    }

    private async Task<CallToolResult> ListDocumentsAsync(CancellationToken cancellationToken)
    {
        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        string? selected = GetSelectedSessionId();
        var verified = await Task.WhenAll(snapshot.Routes.Select(async route => new
        {
            route.Descriptor.DocumentSessionId,
            Diagnostic = await VerifyRouteAsync(snapshot, route, cancellationToken).ConfigureAwait(false)
        })).ConfigureAwait(false);
        var diagnosticBySession = verified.ToDictionary(
            item => item.DocumentSessionId,
            item => item.Diagnostic,
            StringComparer.Ordinal);

        var documents = snapshot.Discovery.Documents.Select(status =>
        {
            RouteEndpointDescriptor? descriptor = status.Descriptor;
            string? diagnostic = null;
            bool attested = descriptor is not null
                && diagnosticBySession.TryGetValue(descriptor.DocumentSessionId, out diagnostic)
                && diagnostic is null;
            return new
            {
                sessionId = descriptor?.DocumentSessionId,
                displayName = descriptor?.DisplayName,
                filePath = descriptor?.FilePath,
                processId = descriptor?.ProcessId,
                runtimeSerialNumber = descriptor?.RuntimeSerialNumber,
                pipeName = descriptor?.PipeName,
                routable = status.Routable && attested,
                selected = descriptor is not null && string.Equals(selected, descriptor.DocumentSessionId, StringComparison.Ordinal),
                status = diagnostic ?? status.Status,
                pluginVersion = descriptor?.PluginVersion,
                descriptorFile = Path.GetFileName(status.DescriptorPath)
            };
        }).ToArray();

        return Success(new
        {
            selectedSessionId = selected,
            canonicalSurfaceFingerprint = snapshot.CanonicalFingerprint,
            documents
        });
    }

    // Attests a canonical route with a probe connection, or reuses this Router's work connection.
    private async Task<string?> VerifyRouteAsync(
        RouterCatalogSnapshot snapshot,
        DocumentRoute route,
        CancellationToken cancellationToken)
    {
        if (route.Surface is null)
        {
            return route.Diagnostic;
        }

        if (!snapshot.IsCanonical(route))
        {
            return "TOOL_SURFACE_MISMATCH";
        }

        try
        {
            BackendLease lease = await _catalog.AcquireAsync(route.Descriptor, BackendAccess.Probe, cancellationToken).ConfigureAwait(false);
            await lease.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (BackendCatalog.IsConnectFailure(ex))
        {
            return BackendCatalog.Diagnose(ex);
        }
    }

    private async Task<CallToolResult> SelectDocumentAsync(
        CallToolRequestParams parameters,
        CancellationToken cancellationToken)
    {
        if (!TryGetStringArgument(parameters, "sessionId", out string? sessionId))
        {
            return RouterErrors.Create("INVALID_DOCUMENT_SESSION", "sessionId is required.");
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        DocumentRoute? route = snapshot.Find(sessionId!);
        if (route is null)
        {
            RouteDocumentStatus? published = snapshot.Discovery.Documents.FirstOrDefault(status =>
                string.Equals(status.Descriptor?.DocumentSessionId, sessionId, StringComparison.Ordinal));
            return published is null
                ? RouterErrors.Create(
                    "DOCUMENT_ENDPOINT_UNAVAILABLE",
                    "The requested document session is not published. List documents again.",
                    retryable: true)
                : RouterErrors.ForDocumentDiagnostic(published.Status);
        }

        (BackendLease? lease, CallToolResult? error) = await AcquireWorkAsync(snapshot, route, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return error!;
        }

        await lease.DisposeAsync().ConfigureAwait(false);
        lock (_selectionLock)
        {
            _selectedSessionId = sessionId;
        }

        return Success(ToDocumentPayload(route.Descriptor, selected: true, "SELECTED"));
    }

    private async Task<CallToolResult> GetSelectedDocumentAsync(CancellationToken cancellationToken)
    {
        string? selectedSessionId = GetSelectedSessionId();
        if (selectedSessionId is null)
        {
            return RouterErrors.Create("NO_DOCUMENT_SELECTED", "No Rhino document is selected for this Router session.");
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        DocumentRoute? route = snapshot.Find(selectedSessionId);
        if (route is null)
        {
            ClearSelectionIf(selectedSessionId);
            return RouterErrors.Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                "The selected Rhino document is no longer open and attested.",
                retryable: true);
        }

        (BackendLease? lease, CallToolResult? error) = await AcquireWorkAsync(snapshot, route, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return error!;
        }

        await lease.DisposeAsync().ConfigureAwait(false);
        return Success(ToDocumentPayload(route.Descriptor, selected: true, "READY"));
    }

    private async Task<CallToolResult> ProxyToolAsync(
        CallToolRequestParams parameters,
        CancellationToken cancellationToken)
    {
        RouterCatalogSnapshot snapshot;
        try
        {
            snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", ex.Message, retryable: true);
        }

        BackendSurface? canonical = snapshot.CanonicalSurface;
        if (canonical is null)
        {
            return RouterErrors.Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                "No compatible saved Rhino document endpoint is available.",
                retryable: true);
        }

        Tool? tool = canonical.Tools.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, parameters.Name, StringComparison.Ordinal));
        if (tool is null)
        {
            return RouterErrors.Create("UNKNOWN_TOOL", $"The canonical Rhino MCP surface does not contain '{parameters.Name}'.");
        }

        if (IsReservedTool(tool.Name))
        {
            return RouterErrors.Create("ROUTER_CONTROL_TOOL_COLLISION", $"The backend tool '{tool.Name}' uses the reserved Router prefix.");
        }

        DocumentRoute? target = ResolveTarget(snapshot, tool, parameters, out CallToolResult? routingError);
        if (target is null)
        {
            return routingError ?? RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", "No document target is available.", retryable: true);
        }

        (BackendLease? lease, CallToolResult? connectError) = await AcquireWorkAsync(snapshot, target, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return connectError!;
        }

        bool mutation = tool.Annotations?.ReadOnlyHint != true;
        await using (lease.ConfigureAwait(false))
        {
            try
            {
                return await lease.Connection.Client.CallToolAsync(parameters, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Without caller cancellation, a cancelled or failed call means the backend went away.
                if (ex is not McpProtocolException)
                {
                    _catalog.Invalidate(lease);
                }

                return mutation
                    ? RouterErrors.Create(
                        "MUTATION_OUTCOME_UNKNOWN",
                        "The routed mutation disconnected after dispatch; inspect the Rhino document before retrying. " + ex.Message,
                        retryable: false,
                        outcomeUnknown: true)
                    : RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", ex.Message, retryable: true);
            }
        }
    }

    // Opens or reuses this Router's work connection and checks that it serves the canonical surface.
    private async Task<(BackendLease? Lease, CallToolResult? Error)> AcquireWorkAsync(
        RouterCatalogSnapshot snapshot,
        DocumentRoute route,
        CancellationToken cancellationToken)
    {
        if (route.Surface is null)
        {
            return (null, RouterErrors.ForDocumentDiagnostic(route.Diagnostic ?? "SURFACE_UNAVAILABLE"));
        }

        if (!snapshot.IsCanonical(route))
        {
            return (null, SurfaceMismatch());
        }

        BackendLease lease;
        try
        {
            lease = await _catalog.AcquireAsync(route.Descriptor, BackendAccess.Work, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (BackendCatalog.IsConnectFailure(ex) || ex is ObjectDisposedException)
        {
            return (null, RouterErrors.ForDocumentDiagnostic(BackendCatalog.Diagnose(ex)));
        }

        BackendSurface? surface = lease.Connection.Surface;
        if (surface is null || !string.Equals(surface.Fingerprint, snapshot.CanonicalFingerprint, StringComparison.Ordinal))
        {
            if (surface is not null)
            {
                _catalog.UpdateSurface(route.Descriptor, surface);
            }

            await lease.DisposeAsync().ConfigureAwait(false);
            return (null, SurfaceMismatch());
        }

        return (lease, null);
    }

    private DocumentRoute? ResolveTarget(
        RouterCatalogSnapshot snapshot,
        Tool tool,
        CallToolRequestParams parameters,
        out CallToolResult? error)
    {
        error = null;
        string? selectedSessionId = GetSelectedSessionId();
        bool hasFilePath = HasTopLevelFilePath(tool);
        TryGetStringArgument(parameters, "filePath", out string? requestedPath);

        if (selectedSessionId is not null)
        {
            DocumentRoute? selected = snapshot.Find(selectedSessionId);
            if (selected is null)
            {
                ClearSelectionIf(selectedSessionId);
                error = RouterErrors.Create(
                    "DOCUMENT_ENDPOINT_UNAVAILABLE",
                    "The selected Rhino document is no longer open and attested.",
                    retryable: true);
                return null;
            }

            if (hasFilePath
                && !string.IsNullOrWhiteSpace(requestedPath)
                && !RoutePath.Equals(requestedPath, selected.Descriptor.FilePath))
            {
                error = RouterErrors.Create(
                    "DOCUMENT_TARGET_CONFLICT",
                    "The supplied filePath does not match the selected Rhino document.");
                return null;
            }

            return selected;
        }

        if (!hasFilePath || string.IsNullOrWhiteSpace(requestedPath))
        {
            error = RouterErrors.Create(
                "NO_DOCUMENT_SELECTED",
                "Select a Rhino document session before calling this tool.");
            return null;
        }

        DocumentRoute[] matches = snapshot.Routes
            .Where(route => RoutePath.Equals(requestedPath, route.Descriptor.FilePath))
            .ToArray();
        if (matches.Length == 0)
        {
            error = RouterErrors.Create("DOCUMENT_NOT_OPEN", "No attested open Rhino document matches filePath.");
            return null;
        }

        if (matches.Length > 1)
        {
            error = RouterErrors.Create(
                "DOCUMENT_TARGET_AMBIGUOUS",
                "The file is open in more than one Rhino process. Select a document session id explicitly.");
            return null;
        }

        return matches[0];
    }

    private static CallToolResult SurfaceMismatch()
    {
        return RouterErrors.Create(
            "TOOL_SURFACE_MISMATCH",
            "The document endpoint does not match the canonical Rhino MCP surface for this Router session.");
    }

    private static IReadOnlyList<Tool> GetCompatibleTools(RouterCatalogSnapshot snapshot)
    {
        IReadOnlyList<Tool> tools = snapshot.CanonicalSurface?.Tools ?? Array.Empty<Tool>();
        if (tools.Any(tool => IsReservedTool(tool.Name)))
        {
            return Array.Empty<Tool>();
        }

        return tools;
    }

    private static bool IsReservedTool(string name) => name.StartsWith(ReservedToolPrefix, StringComparison.Ordinal)
        || name.StartsWith(OfflineToolRegistration.ReservedPrefix, StringComparison.Ordinal);

    private static bool HasTopLevelFilePath(Tool tool)
    {
        JsonElement schema = tool.InputSchema;
        return schema.ValueKind == JsonValueKind.Object
            && schema.TryGetProperty("properties", out JsonElement properties)
            && properties.ValueKind == JsonValueKind.Object
            && properties.TryGetProperty("filePath", out _);
    }

    private static bool TryGetStringArgument(
        CallToolRequestParams parameters,
        string name,
        out string? value)
    {
        value = null;
        if (parameters.Arguments is null
            || !parameters.Arguments.TryGetValue(name, out JsonElement element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static CallToolResult Success(object payload)
    {
        string json = JsonSerializer.Serialize(payload, SerializerOptions);
        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = json }
            },
            StructuredContent = JsonSerializer.SerializeToElement(payload, SerializerOptions)
        };
    }

    private static object ToDocumentPayload(
        RouteEndpointDescriptor descriptor,
        bool selected,
        string status)
    {
        return new
        {
            sessionId = descriptor.DocumentSessionId,
            descriptor.DisplayName,
            descriptor.FilePath,
            descriptor.ProcessId,
            descriptor.RuntimeSerialNumber,
            descriptor.PipeName,
            selected,
            status,
            descriptor.PluginVersion
        };
    }

    private static string GetPublishedSurfaceFingerprint(RouterCatalogSnapshot snapshot)
    {
        return $"router-controls-v1:{snapshot.CanonicalFingerprint ?? "none"}";
    }

    private string? GetSelectedSessionId()
    {
        lock (_selectionLock)
        {
            return _selectedSessionId;
        }
    }

    private void ClearSelectionIf(string sessionId)
    {
        lock (_selectionLock)
        {
            if (string.Equals(_selectedSessionId, sessionId, StringComparison.Ordinal))
            {
                _selectedSessionId = null;
            }
        }
    }

    private void RegisterFrontend(McpServer server)
    {
        lock (_frontendLock)
        {
            _frontendServer = server;
        }
    }

    private static IReadOnlyList<Tool> CreateControlTools()
    {
        JsonElement emptySchema = ParseSchema("""
            {
              "type": "object",
              "properties": {},
              "additionalProperties": false
            }
            """);
        JsonElement selectSchema = ParseSchema("""
            {
              "type": "object",
              "properties": {
                "sessionId": {
                  "type": "string",
                  "description": "Opaque session id returned by rhino_router_list_documents."
                }
              },
              "required": ["sessionId"],
              "additionalProperties": false
            }
            """);

        return new Tool[]
        {
            new()
            {
                Name = ListDocumentsToolName,
                Description = "List saved Rhino documents discovered across all current-user Rhino 8 processes, including route health and opaque session ids.",
                InputSchema = emptySchema,
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = true,
                    DestructiveHint = false,
                    OpenWorldHint = false
                }
            },
            new()
            {
                Name = SelectDocumentToolName,
                Description = "Bind this agent's Router session to one attested open Rhino document without activating a Rhino window or changing the document.",
                InputSchema = selectSchema,
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = false,
                    DestructiveHint = false,
                    OpenWorldHint = false
                }
            },
            new()
            {
                Name = GetSelectedDocumentToolName,
                Description = "Revalidate and return the Rhino document currently selected for this agent's Router session.",
                InputSchema = emptySchema,
                Annotations = new ToolAnnotations
                {
                    ReadOnlyHint = true,
                    DestructiveHint = false,
                    OpenWorldHint = false
                }
            }
        };
    }

    private static JsonElement ParseSchema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
