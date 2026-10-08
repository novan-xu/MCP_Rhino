using System.Windows;

namespace PanelCladdingEditor.UI;

public interface IExtrusionCatalogueClearPrompt
{
    bool ConfirmClear(Window owner, int profileCount);
}

internal sealed class ExtrusionCatalogueClearPrompt : IExtrusionCatalogueClearPrompt
{
    public bool ConfirmClear(Window owner, int profileCount) =>
        MessageBox.Show(owner,
            $"Clear all {profileCount} extrusion profiles, including configured and unconfigured profiles, " +
            "and reset the schedule PDF path?\n\n" +
            "Choose Confirm in Extrusion setup to save the cleared catalogue. Cancel will discard this change.",
            "Clear all extrusions?", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
