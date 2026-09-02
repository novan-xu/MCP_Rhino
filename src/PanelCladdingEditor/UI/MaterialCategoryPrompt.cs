using System.Windows;

namespace PanelCladdingEditor.UI;

public interface IMaterialCategoryPrompt
{
    bool TryGetCategory(Window owner, IReadOnlyCollection<string> existingCategories, out string category);
}

internal sealed class MaterialCategoryPrompt : IMaterialCategoryPrompt
{
    public bool TryGetCategory(
        Window owner,
        IReadOnlyCollection<string> existingCategories,
        out string category)
    {
        var dialog = new MaterialCategoryDialog(existingCategories) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            category = string.Empty;
            return false;
        }

        category = dialog.CategoryName;
        return true;
    }
}
