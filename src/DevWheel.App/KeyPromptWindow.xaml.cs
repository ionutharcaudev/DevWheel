using System.Windows;

namespace DevWheel.App;

/// <summary>Masked key or passphrase prompt. The value is never remembered between uses.</summary>
public partial class KeyPromptWindow : Window
{
    private string? _value;

    private KeyPromptWindow(string title, string hint)
    {
        InitializeComponent();
        TitleText.Text = title;
        HintText.Text = hint;
        DialogPlacement.NearCursor(this);
        Loaded += (_, _) => KeyBox.Focus();
        Closed += (_, _) => KeyBox.Clear();
    }

    public static string? Ask(string title, string hint)
    {
        var window = new KeyPromptWindow(title, hint);
        return window.ShowDialog() == true ? window._value : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (KeyBox.Password.Length == 0) return;
        _value = KeyBox.Password;
        KeyBox.Clear();
        DialogResult = true;
    }
}
