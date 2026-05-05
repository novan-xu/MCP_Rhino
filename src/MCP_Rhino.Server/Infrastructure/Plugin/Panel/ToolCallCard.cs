using Eto.Forms;
using MCP_Rhino.Server.Domain.Enums;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class ToolCallCard : Eto.Forms.Panel
{
    private readonly Label _label = new();

    public ToolCallCard(string toolName, ChatToolCallStatus status, string details)
    {
        _label.Text = $"{toolName} [{status}] {details}";
        Content = _label;
    }
}
