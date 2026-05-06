extern alias rhinocommon;

using MCP_Rhino.Server.Infrastructure.ClaudeCode;
using RhinoDoc = rhinocommon::Rhino.RhinoDoc;
using Panels = rhinocommon::Rhino.UI.Panels;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class RhinoChatPanelHost : IDisposable
{
    private static RhinoChatPanelHost? _current;

    private readonly string _pluginDirectory;
    private readonly Action<string, uint> _startBoundPipeServer;
    private readonly Action<string> _stopBoundPipeServer;
    private readonly Dictionary<uint, PanelChatSessionService> _sessions = new();

    public RhinoChatPanelHost(
        string pluginDirectory,
        Action<string, uint> startBoundPipeServer,
        Action<string> stopBoundPipeServer)
    {
        _pluginDirectory = pluginDirectory;
        _startBoundPipeServer = startBoundPipeServer;
        _stopBoundPipeServer = stopBoundPipeServer;
        _current = this;
    }

    public void StartDocument(RhinoDoc document)
    {
        if (string.IsNullOrWhiteSpace(document.Path))
        {
            return;
        }

        uint serial = document.RuntimeSerialNumber;
        if (_sessions.ContainsKey(serial))
        {
            Panels.OpenPanel(typeof(RhinoChatPanel));
            BindExistingPanel(document);
            return;
        }

        string pipeName = $"mcp_rhino_{serial}";
        _startBoundPipeServer(pipeName, serial);

        string? bridgeExecutablePath = McpConfigBuilder.FindBridgeExecutable(_pluginDirectory);
        var session = new PanelChatSessionService(
            serial,
            document.Path,
            pipeName,
            bridgeExecutablePath,
            new ClaudeCodeProcess());

        _sessions.Add(serial, session);
        Panels.OpenPanel(typeof(RhinoChatPanel));
        BindExistingPanel(document);
    }

    public void UpdateDocumentPath(RhinoDoc document)
    {
        uint serial = document.RuntimeSerialNumber;
        if (!_sessions.TryGetValue(serial, out PanelChatSessionService? session)
            || string.IsNullOrWhiteSpace(document.Path))
        {
            return;
        }

        session.UpdateDocumentPath(document.Path);
        RhinoChatPanel? panel = Panels.GetPanel<RhinoChatPanel>(document);
        panel?.UpdateDocumentPath(document.Path);
    }

    public void StopDocument(uint runtimeSerialNumber, bool closePanel)
    {
        if (!_sessions.Remove(runtimeSerialNumber, out PanelChatSessionService? session))
        {
            return;
        }

        session.Dispose();
        _stopBoundPipeServer(session.PipeName);

        if (closePanel)
        {
            RhinoDoc? document = RhinoDoc.FromRuntimeSerialNumber(runtimeSerialNumber);
            if (document is not null)
            {
                Panels.ClosePanel(typeof(RhinoChatPanel), document);
            }
        }
    }

    public void Dispose()
    {
        foreach (uint serial in _sessions.Keys.ToArray())
        {
            StopDocument(serial, closePanel: true);
        }

        if (ReferenceEquals(_current, this))
        {
            _current = null;
        }
    }

    public static void BindPanel(RhinoChatPanel panel, uint runtimeSerialNumber)
    {
        if (_current is null)
        {
            return;
        }

        if (_current._sessions.TryGetValue(runtimeSerialNumber, out PanelChatSessionService? session))
        {
            panel.BindSession(session);
        }
    }

    public static void UnbindPanel(uint runtimeSerialNumber)
    {
        _current?.StopDocument(runtimeSerialNumber, closePanel: false);
    }

    private void BindExistingPanel(RhinoDoc document)
    {
        RhinoChatPanel? panel = Panels.GetPanel<RhinoChatPanel>(document);
        if (panel is not null && _sessions.TryGetValue(document.RuntimeSerialNumber, out PanelChatSessionService? session))
        {
            panel.BindSession(session);
        }
    }
}
