using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DevWheel.Interop;

namespace DevWheel.App;

/// <summary>Small, non-activating, click-through confirmation near the cursor.</summary>
public static class Toast
{
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromMilliseconds(2500);

    private static Window? _current;

    public static void Show(string message, POINT? at = null) => ShowCore(message, error: false, at);

    public static void ShowError(string message, POINT? at = null) => ShowCore(message, error: true, at);

    private static void ShowCore(string message, bool error, POINT? at)
    {
        _current?.Close();
        var theme = Theme.Current();
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Left = -32000,
            Top = -32000,
            Content = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = error ? theme.ErrorBackground : theme.HubBackground,
                BorderBrush = theme.Separator,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 7),
                Child = new TextBlock
                {
                    Text = message,
                    Foreground = error ? Brushes.White : theme.Foreground,
                    FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                    FontSize = 12.5,
                    MaxWidth = 420,
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        window.SourceInitialized += (_, _) =>
            WindowHelper.SetToolWindowStyle(new WindowInteropHelper(window).Handle, noActivate: true, clickThrough: true);
        window.Show();
        WindowHelper.PlaceNear(new WindowInteropHelper(window).Handle, at ?? WindowHelper.GetCursorPos(), 16, 20);
        _current = window;

        var timer = new DispatcherTimer { Interval = error ? ErrorDuration : SuccessDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!Theme.AnimationsEnabled)
            {
                window.Close();
                return;
            }
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
            fade.Completed += (_, _) => window.Close();
            window.BeginAnimation(UIElement.OpacityProperty, fade);
        };
        window.Closed += (_, _) =>
        {
            timer.Stop();
            if (_current == window) _current = null;
        };
        timer.Start();
    }
}
