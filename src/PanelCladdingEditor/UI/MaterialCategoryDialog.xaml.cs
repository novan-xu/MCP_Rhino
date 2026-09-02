using System.Windows;
using System.Windows.Input;

namespace PanelCladdingEditor.UI;

public partial class MaterialCategoryDialog : Window
{
    private readonly HashSet<string> _existingCategories;

    public MaterialCategoryDialog(IEnumerable<string> existingCategories)
    {
        InitializeComponent();
        _existingCategories = new HashSet<string>(existingCategories, StringComparer.OrdinalIgnoreCase);
        Loaded += (_, _) => CategoryNameText.Focus();
    }

    public string CategoryName { get; private set; } = string.Empty;

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        string category = CategoryNameText.Text.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            ShowValidation("Enter a category name.");
            return;
        }
        if (_existingCategories.Contains(category))
        {
            ShowValidation("That category already exists.");
            return;
        }

        CategoryName = category;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnCategoryNamePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnConfirmClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
        CategoryNameText.Focus();
        CategoryNameText.SelectAll();
    }
}
