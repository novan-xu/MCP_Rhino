using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace MCP_Rhino.Companion;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CompanionOptions _options;
    private readonly Dictionary<string, string?> _selectedModelsByCli = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<CompanionUiEvent> _pendingEvents = new();
    private IAgentSession? _session;
    private string _activeCli = AgentCliCatalog.ClaudeCode;
    private bool _pageReady;

    public MainWindow(CompanionOptions options)
    {
        _options = options;
        _selectedModelsByCli[_activeCli] = AgentCliCatalog.NormalizeModel(options.ModelId);
        _session = CreateSession(_activeCli);
        _session.EventReceived += OnSessionEvent;

        InitializeComponent();

        // Set after InitializeComponent so the XAML Title attribute can't overwrite it.
        Title = $"MCP[{Path.GetFileName(options.DocumentPath)}]";

        // Tie the Companion to the Rhino main window via Win32 owner so the
        // panel follows Rhino's minimize / restore and stays above Rhino —
        // but not above unrelated apps. The pin (📌) button still toggles a
        // global Topmost when the user wants "above everything".
        AttachToRhinoOwner();

        Loaded += OnLoaded;
        Closing += (_, _) => _session?.Dispose();
    }

    private void AttachToRhinoOwner()
    {
        try
        {
            IntPtr rhinoHwnd = FindRhinoMainWindow();
            if (rhinoHwnd == IntPtr.Zero)
            {
                return;
            }

            // Setting WindowInteropHelper.Owner before the window has its
            // own HWND queues the assignment until the HWND is created, so
            // it is safe to call here.
            new WindowInteropHelper(this).Owner = rhinoHwnd;
            ShowInTaskbar = true; // keep our own taskbar entry despite ownership
        }
        catch
        {
            // Non-fatal — companion still works as a standalone window if
            // the owner attach fails (Rhino not running, access denied, etc.).
        }
    }

    private static IntPtr FindRhinoMainWindow()
    {
        // Rhino 8 typically reports its main window under either "Rhino" or
        // "Rhinoceros"; check both. Pick the first non-zero MainWindowHandle.
        foreach (string name in new[] { "Rhino", "Rhinoceros" })
        {
            foreach (Process proc in Process.GetProcessesByName(name))
            {
                try
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        return proc.MainWindowHandle;
                    }
                }
                catch
                {
                    // Process may have exited between enumeration and access.
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        return IntPtr.Zero;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async().ConfigureAwait(true);
            Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            string webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mcp-rhino.local",
                webRoot,
                CoreWebView2HostResourceAccessKind.Allow);
            Browser.Source = new Uri("https://mcp-rhino.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Unable to initialize the MCP_Rhino Companion UI." + Environment.NewLine + ex.Message,
                "MCP_Rhino Companion",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(e.WebMessageAsJson);
            JsonElement root = document.RootElement;
            string? type = root.TryGetProperty("type", out JsonElement typeProperty)
                ? typeProperty.GetString()
                : null;

            switch (type)
            {
                case "ready":
                    _pageReady = true;
                    PostSessionEvent();
                    FlushPendingEvents();
                    if (_session is not null)
                    {
                        await _session.StartAsync(CancellationToken.None).ConfigureAwait(true);
                    }
                    break;

                case "send":
                    if (_session is not null)
                    {
                        if (!CompanionUserMessage.TryReadFromJson(root, out CompanionUserMessage message, out string error))
                        {
                            PostEvent(CompanionUiEvent.Diagnostic(error));
                            PostEvent(CompanionUiEvent.Input(true));
                            break;
                        }

                        await _session.SendUserMessageAsync(message, CancellationToken.None).ConfigureAwait(true);
                    }
                    break;

                case "stop":
                    _session?.Stop();
                    break;

                case "pin":
                    Topmost = root.TryGetProperty("pinned", out JsonElement pinnedProperty)
                        && pinnedProperty.ValueKind == JsonValueKind.True;
                    break;

                case "cli":
                    if (root.TryGetProperty("cli", out JsonElement cliProperty))
                    {
                        string? cli = cliProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(cli))
                        {
                            await SwapSessionAsync(cli).ConfigureAwait(true);
                        }
                    }
                    break;

                case "model":
                    if (root.TryGetProperty("model", out JsonElement modelProperty))
                    {
                        string? model = modelProperty.ValueKind == JsonValueKind.Null
                            ? null
                            : modelProperty.GetString();
                        await SetModelAsync(model).ConfigureAwait(true);
                    }
                    break;
            }
        }
        catch (JsonException ex)
        {
            PostEvent(CompanionUiEvent.Diagnostic("Invalid UI message: " + ex.Message));
        }
        catch (Exception ex)
        {
            PostEvent(CompanionUiEvent.Diagnostic("Companion host error: " + ex.Message));
            PostEvent(CompanionUiEvent.Input(true));
        }
    }

    private async Task SwapSessionAsync(string cli)
    {
        string normalized = AgentCliCatalog.NormalizeCli(cli);
        if (string.Equals(normalized, _activeCli, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RestartSessionAsync(normalized).ConfigureAwait(false);
    }

    private async Task SetModelAsync(string? model)
    {
        string? normalizedModel = AgentCliCatalog.NormalizeModel(model);
        string? currentModel = SelectedModelFor(_activeCli);
        if (string.Equals(normalizedModel, currentModel, StringComparison.OrdinalIgnoreCase))
        {
            PostSessionEvent();
            return;
        }

        _selectedModelsByCli[_activeCli] = normalizedModel;
        await RestartSessionAsync(_activeCli).ConfigureAwait(false);
    }

    private async Task RestartSessionAsync(string cli)
    {
        string normalized = AgentCliCatalog.NormalizeCli(cli);
        if (_session is not null)
        {
            _session.EventReceived -= OnSessionEvent;
            try { _session.Stop(); } catch { /* best-effort */ }
            try { _session.Dispose(); } catch { /* best-effort */ }
        }

        _activeCli = normalized;
        _session = CreateSession(normalized);
        _session.EventReceived += OnSessionEvent;

        PostSessionEvent();
        await _session.StartAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private IAgentSession CreateSession(string cli)
    {
        CompanionOptions sessionOptions = _options with { ModelId = SelectedModelFor(cli) };
        return string.Equals(cli, AgentCliCatalog.Codex, StringComparison.OrdinalIgnoreCase)
            ? new CodexCliSession(sessionOptions)
            : new ClaudeCodeSession(sessionOptions);
    }

    private string? SelectedModelFor(string cli)
    {
        return _selectedModelsByCli.TryGetValue(AgentCliCatalog.NormalizeCli(cli), out string? model)
            ? AgentCliCatalog.NormalizeModel(model)
            : null;
    }

    private void PostSessionEvent()
    {
        string? model = SelectedModelFor(_activeCli);
        CompanionOptions sessionOptions = _options with { ModelId = model };
        PostEvent(CompanionUiEvent.Session(
            sessionOptions,
            _activeCli,
            AgentCliCatalog.ModelsFor(_activeCli, model)));
    }

    private void OnSessionEvent(object? sender, CompanionUiEvent uiEvent)
    {
        Dispatcher.Invoke(() => PostEvent(uiEvent));
    }

    private void PostEvent(CompanionUiEvent uiEvent)
    {
        if (!_pageReady || Browser.CoreWebView2 is null)
        {
            _pendingEvents.Enqueue(uiEvent);
            return;
        }

        string json = JsonSerializer.Serialize(uiEvent, JsonOptions);
        Browser.CoreWebView2.PostWebMessageAsJson(json);
    }

    private void FlushPendingEvents()
    {
        while (_pendingEvents.Count > 0)
        {
            PostEvent(_pendingEvents.Dequeue());
        }
    }
}
