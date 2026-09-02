extern alias rhinocommon;

using System.Windows;
using System.Windows.Media;
using RhinoColor4f = rhinocommon::Rhino.Display.Color4f;
using RhinoDialogs = rhinocommon::Rhino.UI.Dialogs;

namespace PanelCladdingEditor.UI;

public interface IMaterialColorPicker
{
    bool TryPick(Window owner, Color initialColor, out Color selectedColor);
}

internal sealed class RhinoMaterialColorPicker : IMaterialColorPicker
{
    public bool TryPick(Window owner, Color initialColor, out Color selectedColor)
    {
        var choice = new RhinoColor4f(System.Drawing.Color.FromArgb(
            initialColor.R,
            initialColor.G,
            initialColor.B));

        if (!RhinoDialogs.ShowColorDialog((object)owner, ref choice, allowAlpha: false))
        {
            selectedColor = initialColor;
            return false;
        }

        selectedColor = Color.FromRgb(ToByte(choice.R), ToByte(choice.G), ToByte(choice.B));
        return true;
    }

    private static byte ToByte(float component) =>
        (byte)Math.Round(Math.Clamp(component, 0f, 1f) * byte.MaxValue);
}
