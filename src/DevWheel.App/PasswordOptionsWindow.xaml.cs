using System.Globalization;
using System.Windows;
using DevWheel.Core;

namespace DevWheel.App;

/// <summary>Length and character sets for one password (Shift+click on the tool).</summary>
public partial class PasswordOptionsWindow : Window
{
    private PasswordOptions? _result;

    private PasswordOptionsWindow(PasswordOptions defaults)
    {
        InitializeComponent();
        LengthBox.Text = defaults.Length.ToString(CultureInfo.InvariantCulture);
        RangeText.Text = $"({PasswordOptions.MinLength}-{PasswordOptions.MaxLength})";
        UpperBox.IsChecked = defaults.Upper;
        LowerBox.IsChecked = defaults.Lower;
        DigitsBox.IsChecked = defaults.Digits;
        SymbolsBox.IsChecked = defaults.Symbols;
        DialogPlacement.NearCursor(this);
        Loaded += (_, _) =>
        {
            LengthBox.Focus();
            LengthBox.SelectAll();
        };
    }

    public static PasswordOptions? Ask(PasswordOptions defaults)
    {
        var window = new PasswordOptionsWindow(defaults);
        return window.ShowDialog() == true ? window._result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LengthBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            ErrorText.Text = "Length must be a number";
            return;
        }
        var options = new PasswordOptions(length, UpperBox.IsChecked == true, LowerBox.IsChecked == true,
            DigitsBox.IsChecked == true, SymbolsBox.IsChecked == true);
        if (options.Validate() is { } error)
        {
            ErrorText.Text = error;
            return;
        }
        _result = options;
        DialogResult = true;
    }
}
