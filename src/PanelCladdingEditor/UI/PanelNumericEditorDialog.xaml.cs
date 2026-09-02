using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace PanelCladdingEditor.UI;

public partial class PanelNumericEditorDialog : Window
{
    private readonly double _minimum;
    private readonly double _maximum;
    private readonly bool _exclusiveBounds;

    public PanelNumericEditorDialog(
        string title,
        string subtitle,
        string label,
        double initialValue,
        double minimum,
        double maximum,
        string actionText,
        bool exclusiveBounds)
    {
        InitializeComponent();
        _minimum = minimum;
        _maximum = maximum;
        _exclusiveBounds = exclusiveBounds;
        Title = title;
        DialogTitleText.Text = title;
        DialogSubtitleText.Text = subtitle;
        ValueLabelText.Text = label;
        ValueText.Text = initialValue.ToString("0.00000", CultureInfo.InvariantCulture);
        RangeHintText.Text = exclusiveBounds
            ? $"Must fall between {minimum:0.00000} and {maximum:0.00000}."
            : $"Allowed range: {minimum:0.00000} to {maximum:0.00000}.";
        ConfirmButton.Content = actionText;
        Loaded += (_, _) =>
        {
            ValueText.Focus();
            ValueText.SelectAll();
        };
    }

    public double Value { get; private set; }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(ValueText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            ShowValidation("Enter a valid numeric value using a period as the decimal separator.");
            return;
        }
        bool outside = _exclusiveBounds
            ? value <= _minimum || value >= _maximum
            : value < _minimum || value > _maximum;
        if (outside)
        {
            ShowValidation(_exclusiveBounds
                ? "The value must fall strictly inside the stated range."
                : "The value falls outside the stated range.");
            return;
        }
        Value = Math.Round(value, 5, MidpointRounding.AwayFromZero);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnValuePreviewKeyDown(object sender, KeyEventArgs e)
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
        ValueText.Focus();
        ValueText.SelectAll();
    }
}
