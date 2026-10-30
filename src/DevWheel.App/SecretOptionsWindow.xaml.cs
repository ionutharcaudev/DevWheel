using System.Windows;
using DevWheel.Core;

namespace DevWheel.App;

/// <summary>Length and encoding for one random secret (Shift+click on the tool).</summary>
public partial class SecretOptionsWindow : Window
{
    private SecretOptions? _result;

    private SecretOptionsWindow(SecretOptions defaults)
    {
        InitializeComponent();
        Bytes16.IsChecked = defaults.Bytes == 16;
        Bytes32.IsChecked = defaults.Bytes == 32;
        Bytes64.IsChecked = defaults.Bytes == 64;
        FormatHex.IsChecked = defaults.Format == SecretFormat.Hex;
        FormatBase64.IsChecked = defaults.Format == SecretFormat.Base64;
        DialogPlacement.NearCursor(this);
    }

    public static SecretOptions? Ask(SecretOptions defaults)
    {
        var window = new SecretOptionsWindow(defaults);
        return window.ShowDialog() == true ? window._result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var bytes = Bytes16.IsChecked == true ? 16 : Bytes64.IsChecked == true ? 64 : 32;
        _result = new SecretOptions(bytes, FormatBase64.IsChecked == true ? SecretFormat.Base64 : SecretFormat.Hex);
        DialogResult = true;
    }
}
