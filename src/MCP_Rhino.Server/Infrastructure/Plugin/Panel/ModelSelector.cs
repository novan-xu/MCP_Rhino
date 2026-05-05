using Eto.Forms;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Panel;

public sealed class ModelSelector : Eto.Forms.Panel
{
    private readonly DropDown _dropDown = new();

    public ModelSelector()
    {
        _dropDown.Items.Add("Default (CC settings)");
        _dropDown.Items.Add("claude-haiku-4-5");
        _dropDown.Items.Add("claude-sonnet-4-6");
        _dropDown.Items.Add("claude-opus-4-7");
        _dropDown.SelectedIndex = 0;
        Content = _dropDown;
    }

    public string? SelectedModelId => _dropDown.SelectedIndex <= 0
        ? null
        : _dropDown.SelectedValue?.ToString();
}
