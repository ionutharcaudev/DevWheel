using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace DevWheel.App;

/// <summary>Colors for the wheel and toasts, following the Windows app theme and accent color.</summary>
public sealed class Theme
{
    private Theme(bool dark, Color accent)
    {
        IsDark = dark;
        Accent = accent;
        Foreground = Freeze(dark ? Color.FromRgb(0xF3, 0xF3, 0xF3) : Color.FromRgb(0x1B, 0x1B, 0x1B));
        SubtleForeground = Freeze(dark ? Color.FromRgb(0xB8, 0xB8, 0xB8) : Color.FromRgb(0x5C, 0x5C, 0x5C));
        RingBackground = Freeze(dark ? Color.FromArgb(0xE8, 0x20, 0x20, 0x20) : Color.FromArgb(0xE8, 0xF3, 0xF3, 0xF3));
        HubBackground = Freeze(dark ? Color.FromArgb(0xF5, 0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF));
        Separator = Freeze(dark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x30, 0x00, 0x00, 0x00));
        GroupHover = Freeze(Color.FromArgb(0x55, accent.R, accent.G, accent.B));
        ToolHover = Freeze(Color.FromArgb(0xB0, accent.R, accent.G, accent.B));
        AccentBrush = Freeze(accent);
        ErrorBackground = Freeze(Color.FromArgb(0xF5, 0xC4, 0x2B, 0x1C));
        // Nearly transparent, but not fully: transparent pixels of a layered window do not receive the mouse.
        HitTestFill = Freeze(Color.FromArgb(0x01, 0, 0, 0));
    }

    public bool IsDark { get; }
    public Color Accent { get; }
    public Brush Foreground { get; }
    public Brush SubtleForeground { get; }
    public Brush RingBackground { get; }
    public Brush HubBackground { get; }
    public Brush Separator { get; }
    public Brush GroupHover { get; }
    public Brush ToolHover { get; }
    public Brush AccentBrush { get; }
    public Brush ErrorBackground { get; }
    public Brush HitTestFill { get; }

    public static FontFamily IconFont { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>The Windows "Show animations" setting.</summary>
    public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    public static Theme Current()
    {
        var dark = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (System.Security.SecurityException)
        {
        }

        var glass = SystemParameters.WindowGlassColor;
        var accent = glass.A == 0 ? Color.FromRgb(0x00, 0x78, 0xD4) : Color.FromRgb(glass.R, glass.G, glass.B);
        return new Theme(dark, accent);
    }

    private static SolidColorBrush Freeze(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }
}
