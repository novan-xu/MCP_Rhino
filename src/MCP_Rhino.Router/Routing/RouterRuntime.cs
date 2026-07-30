using System.Text.Json;
using MCP_Rhino.Transport.Routing;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

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
            Tools = ControlTools.Concat(backendTools).ToList()
        };
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken)
    {
        RegisterFrontend(request.Server);
        return await CallToolCoreAsync(request.Params, cancellationToken).ConfigureAwait(false);
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
            Resources = snapshot.CanonicalBackend?.Resources.ToList() ?? new List<Resource>()
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
            ResourceTemplates = snapshot.CanonicalBackend?.ResourceTemplates.ToList() ?? new List<ResourceTemplate>()
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
        BackendConnection backend = snapshot.CanonicalBackend
            ?? throw new McpProtocolException("No compatible saved Rhino document endpoint is available.");
        return await backend.Client.ReadResourceAsync(request.Params, cancellationToken).ConfigureAwait(false);
    }

    public async Task CheckForToolSurfaceChangeAsync(CancellationToken cancellationToken)
    {
        McpServer? frontend;
        string? published;
        lock (_frontendLock)
        {
            frontend = _frontendServer;
            published = _publishedSurfaceFingerprint;
        }

        if (frontend is null || published is null)
        {
            return;
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        string current = GetPublishedSurfaceFingerprint(snapshot);
        if (string.Equals(current, published, StringComparison.Ordinal))
        {
            return;
        }

        lock (_frontendLock)
        {
            if (!string.Equals(_publishedSurfaceFingerprint, published, StringComparison.Ordinal))
            {
                return;
            }

            _publishedSurfaceFingerprint = current;
        }

        await frontend.SendNotificationAsync(
            NotificationMethods.ToolListChangedNotification,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CallToolResult> ListDocumentsAsync(CancellationToken cancellationToken)
    {
        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        string? selected = GetSelectedSessionId();
        var connectionBySession = snapshot.ConnectionResults.ToDictionary(
            result => result.Descriptor.DocumentSessionId,
            StringComparer.Ordinal);

        var documents = snapshot.Discovery.Documents.Select(status =>
        {
            RouteEndpointDescriptor? descriptor = status.Descriptor;
            BackendConnectionResult? connection = descriptor is not null
                && connectionBySession.TryGetValue(descriptor.DocumentSessionId, out BackendConnectionResult? found)
                    ? found
                    : null;
            bool canonical = connection?.Connection is not null
                && snapshot.CanonicalConnections.Contains(connection.Connection);
            return new
            {
                sessionId = descriptor?.DocumentSessionId,
                displayName = descriptor?.DisplayName,
                filePath = descriptor?.FilePath,
                processId = descriptor?.ProcessId,
                runtimeSerialNumber = descriptor?.RuntimeSerialNumber,
                pipeName = descriptor?.PipeName,
                routable = status.Routable && connection?.Connection is not null && canonical,
                selected = descriptor is not null && string.Equals(selected, descriptor.DocumentSessionId, StringComparison.Ordinal),
                status = connection?.Diagnostic
                    ?? (connection?.Connection is not null && !canonical ? "TOOL_SURFACE_MISMATCH" : status.Status),
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

    private async Task<CallToolResult> SelectDocumentAsync(
        CallToolRequestParams parameters,
        CancellationToken cancellationToken)
    {
        if (!TryGetStringArgument(parameters, "sessionId", out string? sessionId))
        {
            return RouterErrors.Create("INVALID_DOCUMENT_SESSION", "sessionId is required.");
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        BackendConnection? selected = snapshot.CanonicalConnections.FirstOrDefault(connection =>
            string.Equals(connection.Descriptor.DocumentSessionId, sessionId, StringComparison.Ordinal));
        if (selected is null)
        {
            bool incompatible = snapshot.HealthyConnections.Any(connection =>
                string.Equals(connection.Descriptor.DocumentSessionId, sessionId, StringComparison.Ordinal));
            return RouterErrors.Create(
                incompatible ? "TOOL_SURFACE_MISMATCH" : "DOCUMENT_ENDPOINT_UNAVAILABLE",
                incompatible
                    ? "The document endpoint does not match the canonical Rhino MCP surface for this Router session."
                    : "The requested document session is not open and attested.",
                retryable: !incompatible);
        }

        lock (_selectionLock)
        {
            _selectedSessionId = sessionId;
        }

        return Success(ToDocumentPayload(selected.Descriptor, selected: true, "SELECTED"));
    }

    private async Task<CallToolResult> GetSelectedDocumentAsync(CancellationToken cancellationToken)
    {
        string? selectedSessionId = GetSelectedSessionId();
        if (selectedSessionId is null)
        {
            return RouterErrors.Create("NO_DOCUMENT_SELECTED", "No Rhino document is selected for this Router session.");
        }

        RouterCatalogSnapshot snapshot = await _catalog.RefreshAsync(cancellationToken).ConfigureAwait(false);
        BackendConnection? selected = snapshot.CanonicalConnections.FirstOrDefault(connection =>
            string.Equals(connection.Descriptor.DocumentSessionId, selectedSessionId, StringComparison.Ordinal));
        if (selected is null)
        {
            ClearSelectionIf(selectedSessionId);
            return RouterErrors.Create(
                "DOCUMENT_ENDPOINT_UNAVAILABLE",
                "The selected Rhino document is no longer open and attested.",
                retryable: true);
        }

        return Success(ToDocumentPayload(selected.Descriptor, selected: true, "READY"));
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", ex.Message, retryable: true);
        }

        BackendConnection? canonical = snapshot.CanonicalBackend;
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

        if (tool.Name.StartsWith(ReservedToolPrefix, StringComparison.Ordinal))
        {
            return RouterErrors.Create("ROUTER_CONTROL_TOOL_COLLISION", $"The backend tool '{tool.Name}' uses the reserved Router prefix.");
        }

        BackendConnection? target = ResolveTarget(snapshot, tool, parameters, out CallToolResult? routingError);
        if (target is null)
        {
            return routingError ?? RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", "No document target is available.", retryable: true);
        }

        bool mutation = tool.Annotations?.ReadOnlyHint != true;
        bool dispatched = false;
        try
        {
            dispatched = true;
            return await target.Client.CallToolAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or ModelContextProtocol.McpException)
        {
            return mutation && dispatched
                ? RouterErrors.Create(
                    "MUTATION_OUTCOME_UNKNOWN",
                    "The routed mutation disconnected after dispatch; inspect the Rhino document before retrying. " + ex.Message,
                    retryable: false,
                    outcomeUnknown: true)
                : RouterErrors.Create("DOCUMENT_ENDPOINT_UNAVAILABLE", ex.Message, retryable: true);
        }
    }

    private BackendConnection? ResolveTarget(
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
            BackendConnection? selected = snapshot.CanonicalConnections.FirstOrDefault(connection =>
                string.Equals(connection.Descriptor.DocumentSessionId, selectedSessionId, StringComparison.Ordinal));
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

        BackendConnection[] matches = snapshot.CanonicalConnections
            .Where(connection => RoutePath.Equals(requestedPath, connection.Descriptor.FilePath))
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

    private static IReadOnlyList<Tool> GetCompatibleTools(RouterCatalogSnapshot snapshot)
    {
        IReadOnlyList<Tool> tools = snapshot.CanonicalBackend?.Tools ?? Array.Empty<Tool>();
        if (tools.Any(tool => tool.Name.StartsWith(ReservedToolPrefix, StringComparison.Ordinal)))
        {
            return Array.Empty<Tool>();
        }

        return tools;
    }

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
