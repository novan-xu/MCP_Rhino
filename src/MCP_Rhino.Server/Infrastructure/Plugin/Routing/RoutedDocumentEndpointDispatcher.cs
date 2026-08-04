extern alias rhinocommon;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MCP_Rhino.Transport.Routing;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Routing;

public sealed class RoutedDocumentEndpointDispatcher : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly McpRhinoPlugin _plugin;
    private readonly RouteRegistry _registry;
    private readonly Dictionary<uint, RoutedDocumentState> _states = new();
    private readonly string _pluginInstanceGeneration = Guid.NewGuid().ToString("N");
    private readonly int _processId = Environment.ProcessId;
    private readonly long _processStartTimeUtcTicks;
    private readonly string _pluginVersion;
    private long _nextLifecycleGeneration;
    private bool _started;
    private bool _disposed;

    public RoutedDocumentEndpointDispatcher(McpRhinoPlugin plugin, RouteRegistry? registry = null)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _registry = registry ?? new RouteRegistry();
        using Process process = Process.GetCurrentProcess();
        _processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
        _pluginVersion = typeof(McpRhinoPlugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        RhinoDoc.NewDocument += OnDocumentChanged;
        RhinoDoc.EndOpenDocument += OnDocumentChanged;
        RhinoDoc.EndSaveDocument += OnDocumentChanged;
        RhinoDoc.CloseDocument += OnDocumentClosed;

        foreach (RhinoDoc document in GetOpenDocuments())
        {
            ReconcileDocument(document);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_started)
        {
            RhinoDoc.NewDocument -= OnDocumentChanged;
            RhinoDoc.EndOpenDocument -= OnDocumentChanged;
            RhinoDoc.EndSaveDocument -= OnDocumentChanged;
            RhinoDoc.CloseDocument -= OnDocumentClosed;
            _started = false;
        }

        RoutedDocumentState[] states = _states.Values.ToArray();
        foreach (RoutedDocumentState state in states)
        {
            state.Closing = true;
            TryWriteDescriptor(state, routable: false, status: "PLUGIN_SHUTDOWN");
        }

        _plugin.StopAllRoutedPipeServers();
        foreach (RoutedDocumentState state in states)
        {
            _registry.DeleteIfGenerationMatches(CreateDescriptor(state, false, "PLUGIN_SHUTDOWN"));
        }

        _states.Clear();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        ReconcileDocument(GetDocument(sender, e));
    }

    private void OnDocumentClosed(object? sender, EventArgs e)
    {
        RhinoDoc? document = GetDocument(sender, e);
        if (document is null || !_states.Remove(document.RuntimeSerialNumber, out RoutedDocumentState? state))
        {
            return;
        }

        state.Closing = true;
        TryWriteDescriptor(state, routable: false, status: "DOCUMENT_CLOSING");
        if (state.ListenerStarted)
        {
            _plugin.StopRoutedPipeServer(state.PipeName);
            state.ListenerStarted = false;
        }

        _registry.DeleteIfGenerationMatches(CreateDescriptor(state, false, "DOCUMENT_CLOSED"));
    }

    private void ReconcileDocument(RhinoDoc? document)
    {
        if (_disposed || document is null)
        {
            return;
        }

        uint serial = document.RuntimeSerialNumber;
        if (!_states.TryGetValue(serial, out RoutedDocumentState? state))
        {
            state = new RoutedDocumentState
            {
                RuntimeSerialNumber = serial,
                PipeName = McpPipeNames.ForRoutedDocument(serial),
                DocumentSessionId = Guid.NewGuid().ToString("N"),
                LifecycleGeneration = Interlocked.Increment(ref _nextLifecycleGeneration),
                EndpointNonce = Guid.NewGuid().ToString("N")
            };
            _states.Add(serial, state);
        }

        if (state.Closing)
        {
            return;
        }

        string? documentPath = string.IsNullOrWhiteSpace(document.Path)
            ? null
            : RoutePath.Normalize(document.Path);
        state.FilePath = documentPath;
        state.DisplayName = !string.IsNullOrWhiteSpace(document.Name)
            ? document.Name
            : documentPath is not null
                ? Path.GetFileName(documentPath)
                : $"Untitled {serial}";

        if (documentPath is null)
        {
            if (state.ListenerStarted)
            {
                _plugin.StopRoutedPipeServer(state.PipeName);
                state.ListenerStarted = false;
            }

            TryWriteDescriptor(state, routable: false, status: "ACTIVE_DOC_UNSAVED");
            return;
        }

        string attestationJson = JsonSerializer.Serialize(CreateAttestation(state), SerializerOptions);
        if (!state.ListenerStarted)
        {
            state.EndpointNonce = Guid.NewGuid().ToString("N");
            attestationJson = JsonSerializer.Serialize(CreateAttestation(state), SerializerOptions);
            state.ListenerStarted = _plugin.StartRoutedPipeServer(state.PipeName, serial, attestationJson);
        }
        else
        {
            _plugin.UpdateRoutedPipeAttestation(state.PipeName, attestationJson);
        }

        TryWriteDescriptor(
            state,
            routable: state.ListenerStarted,
            status: state.ListenerStarted ? "READY" : "LISTENER_START_FAILED");
    }

    private void TryWriteDescriptor(RoutedDocumentState state, bool routable, string status)
    {
        try
        {
            _registry.Write(CreateDescriptor(state, routable, status));
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP_Rhino route descriptor update failed for {state.RuntimeSerialNumber}: {ex.Message}");
            if (state.ListenerStarted)
            {
                _plugin.StopRoutedPipeServer(state.PipeName);
                state.ListenerStarted = false;
            }
        }
    }

    private RouteEndpointDescriptor CreateDescriptor(RoutedDocumentState state, bool routable, string status)
    {
        return new RouteEndpointDescriptor
        {
            DocumentSessionId = state.DocumentSessionId,
            PluginInstanceGeneration = _pluginInstanceGeneration,
            DocumentLifecycleGeneration = state.LifecycleGeneration,
            EndpointNonce = state.EndpointNonce,
            ProcessId = _processId,
            ProcessStartTimeUtcTicks = _processStartTimeUtcTicks,
            RuntimeSerialNumber = state.RuntimeSerialNumber,
            PipeName = state.PipeName,
            FilePath = state.FilePath,
            DisplayName = state.DisplayName,
            Routable = routable,
            PluginVersion = _pluginVersion,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Status = status
        };
    }

    private RouteEndpointAttestation CreateAttestation(RoutedDocumentState state)
    {
        return new RouteEndpointAttestation
        {
            DocumentSessionId = state.DocumentSessionId,
            PluginInstanceGeneration = _pluginInstanceGeneration,
            DocumentLifecycleGeneration = state.LifecycleGeneration,
            EndpointNonce = state.EndpointNonce,
            ProcessId = _processId,
            ProcessStartTimeUtcTicks = _processStartTimeUtcTicks,
            RuntimeSerialNumber = state.RuntimeSerialNumber,
            PipeName = state.PipeName,
            FilePath = state.FilePath ?? string.Empty,
            ServerVersion = _pluginVersion
        };
    }

    private static IEnumerable<RhinoDoc> GetOpenDocuments()
    {
        foreach (RhinoDoc document in RhinoDoc.OpenDocuments())
        {
            yield return document;
        }
    }

    private static RhinoDoc? GetDocument(object? sender, EventArgs e)
    {
        if (sender is RhinoDoc senderDocument)
        {
            return senderDocument;
        }

        object? document = e.GetType().GetProperty("Document", BindingFlags.Instance | BindingFlags.Public)?.GetValue(e);
        return document as RhinoDoc;
    }

    private sealed class RoutedDocumentState
    {
        public required string DocumentSessionId { get; init; }

        public required string PipeName { get; init; }

        public required string EndpointNonce { get; set; }

        public long LifecycleGeneration { get; init; }

        public uint RuntimeSerialNumber { get; init; }

        public string? FilePath { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public bool ListenerStarted { get; set; }

        public bool Closing { get; set; }
    }
}
