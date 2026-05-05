extern alias rhinocommon;

using Eto.Forms;
using MCP_Rhino.Server.Domain.Enums;
using RhinoApp = rhinocommon::Rhino.RhinoApp;
using IPanel = rhinocommon::Rhino.UI.IPanel;
using ShowPanelReason = rhinocommon::Rhino.UI.ShowPanelReason;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class RhinoChatPanel : Eto.Forms.Panel, IPanel
{
    private readonly Label _header = new() { Text = "Claude Code Chat" };
    private readonly ModelSelector _modelSelector = new();
    private readonly ChatTranscriptView _transcript = new();
    private readonly ChatInputBox _input = new();
    private IPanelChatSession? _session;

    public RhinoChatPanel()
    {
        _input.SendRequested += async (_, _) => await SendAsync().ConfigureAwait(false);
        _input.StopRequested += (_, _) => _session?.Stop();
        _input.SetInputEnabled(false);

        var headerLayout = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Items = { _header, _modelSelector }
        };

        var layout = new DynamicLayout
        {
            Padding = new Eto.Drawing.Padding(8),
            Spacing = new Eto.Drawing.Size(6, 6)
        };
        layout.Add(headerLayout);
        layout.Add(_transcript, yscale: true);
        layout.Add(_input);
        Content = layout;
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
        RhinoChatPanelHost.BindPanel(this, documentSerialNumber);
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
    {
    }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument)
    {
        RhinoChatPanelHost.UnbindPanel(documentSerialNumber);
    }

    public void BindSession(IPanelChatSession session)
    {
        if (_session is not null)
        {
            _session.EventReceived -= OnSessionEvent;
        }

        _session = session;
        _session.EventReceived += OnSessionEvent;
        _header.Text = $"Claude Code Chat - {Path.GetFileName(session.DocumentPath)}";
        _input.SetInputEnabled(session.IsInputEnabled);
        _ = session.StartAsync(_modelSelector.SelectedModelId, CancellationToken.None);
    }

    public void UpdateDocumentPath(string documentPath)
    {
        _header.Text = $"Claude Code Chat - {Path.GetFileName(documentPath)}";
    }

    private async Task SendAsync()
    {
        if (_session is null)
        {
            return;
        }

        string text = _input.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _input.Text = string.Empty;
        await _session.SendUserMessageAsync(text, CancellationToken.None).ConfigureAwait(false);
    }

    private void OnSessionEvent(object? sender, PanelChatEvent chatEvent)
    {
        void UpdateUi()
        {
            if (!string.IsNullOrWhiteSpace(chatEvent.ToolName))
            {
                string detail = string.IsNullOrWhiteSpace(chatEvent.Text) ? string.Empty : chatEvent.Text;
                var card = new ToolCallCard(
                    chatEvent.ToolName,
                    chatEvent.ToolStatus ?? ChatToolCallStatus.Running,
                    detail);
                _transcript.Append("Tool", card.Content is Label label ? label.Text : chatEvent.ToolName);
            }
            else
            {
                _transcript.Append(chatEvent.Role.ToString(), chatEvent.Text);
            }

            if (_session is not null)
            {
                _input.SetInputEnabled(_session.IsInputEnabled);
            }
        }

        if (RhinoApp.InvokeRequired)
        {
            RhinoApp.InvokeOnUiThread(new Action(UpdateUi));
        }
        else
        {
            UpdateUi();
        }
    }
}
