using System;
using System.Windows;

namespace DevWheel.App;

/// <summary>Read-only viewer for JWT and timestamp results, with a Copy button.</summary>
public partial class ViewerWindow : Window
{
    private readonly Action<string> _copy;

    private ViewerWindow(string title, string content, Action<string> copy)
    {
        InitializeComponent();
        Title = title;
        ContentBox.Text = content;
        _copy = copy;
        DialogPlacement.NearCursor(this);
        Loaded += (_, _) => ContentBox.Focus();
        Closed += (_, _) => ContentBox.Clear();
    }

    public static void Open(string title, string content, Action<string> copy)
    {
        var window = new ViewerWindow(title, content, copy);
        window.Show();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var text = ContentBox.SelectionLength > 0 ? ContentBox.SelectedText : ContentBox.Text;
        _copy(text);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
