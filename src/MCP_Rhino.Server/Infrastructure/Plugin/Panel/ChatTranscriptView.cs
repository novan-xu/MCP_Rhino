using Eto.Forms;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class ChatTranscriptView : Eto.Forms.Panel
{
    private readonly TextArea _textArea = new()
    {
        ReadOnly = true,
        Wrap = true
    };

    public ChatTranscriptView()
    {
        Content = _textArea;
    }

    public void Append(string prefix, string text)
    {
        string line = string.IsNullOrWhiteSpace(prefix) ? text : $"{prefix}: {text}";
        _textArea.Text = string.IsNullOrWhiteSpace(_textArea.Text)
            ? line
            : _textArea.Text + Environment.NewLine + Environment.NewLine + line;
        _textArea.CaretIndex = _textArea.Text.Length;
    }

    public void Clear()
    {
        _textArea.Text = string.Empty;
    }
}
