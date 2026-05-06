using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MCP_Rhino.Companion;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CompanionOptions _options;
    private readonly ClaudeCodeSession _session;
    private readonly Queue<CompanionUiEvent> _pendingEvents = new();
    private bool _pageReady;

    public MainWindow(CompanionOptions options)
    {
        _options = options;
        _session = new ClaudeCodeSession(options);
        _session.EventReceived += OnSessionEvent;
        Title = $"MCP_Rhino Companion - {Path.GetFileName(options.DocumentPath)}";

        InitializeComponent();
        Loaded += OnLoaded;
        Closing += (_, _) => _session.Dispose();
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
                    PostEvent(CompanionUiEvent.Session(_options));
                    FlushPendingEvents();
                    await _session.StartAsync(CancellationToken.None).ConfigureAwait(false);
                    break;

                case "send":
                    if (root.TryGetProperty("text", out JsonElement textProperty))
                    {
                        string? text = textProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            await _session.SendUserMessageAsync(text, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    break;

                case "stop":
                    _session.Stop();
                    break;
            }
        }
        catch (JsonException ex)
        {
            PostEvent(CompanionUiEvent.Diagnostic("Invalid UI message: " + ex.Message));
        }
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
