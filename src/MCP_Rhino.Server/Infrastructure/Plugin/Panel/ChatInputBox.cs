using Eto.Forms;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class ChatInputBox : Eto.Forms.Panel
{
    private readonly TextArea _input = new() { Wrap = true };
    private readonly Button _sendButton = new() { Text = "Send" };
    private readonly Button _stopButton = new() { Text = "Stop" };

    public ChatInputBox()
    {
        _sendButton.Click += (_, _) => SendRequested?.Invoke(this, EventArgs.Empty);
        _stopButton.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

        var buttons = new StackLayout
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Items = { _sendButton, _stopButton }
        };

        var layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(4, 4) };
        layout.Add(_input, yscale: true);
        layout.Add(buttons);
        Content = layout;
    }

    public event EventHandler? SendRequested;

    public event EventHandler? StopRequested;

    public string Text
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    public void SetInputEnabled(bool enabled)
    {
        _input.Enabled = enabled;
        _sendButton.Enabled = enabled;
        _stopButton.Enabled = true;
    }
}
