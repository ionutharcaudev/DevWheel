using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DevWheel.App;

/// <summary>Tray icon with Open settings, Pause hotkey, About and Exit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _image;
    private readonly ToolStripMenuItem _pause;
    private readonly WheelController _controller;

    public TrayIcon(WheelController controller, Action exit)
    {
        _controller = controller;
        _image = DrawIcon();
        _pause = new ToolStripMenuItem("Pause hotkey") { CheckOnClick = true };
        _pause.CheckedChanged += (_, _) => _controller.SetPaused(_pause.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => _controller.OpenSettings());
        menu.Items.Add(_pause);
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = "DevWheel",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _controller.OpenSettings();
        controller.HotkeyChanged += UpdateTooltip;
        UpdateTooltip();
    }

    public void ShowBalloon(string message) => _icon.ShowBalloonTip(4000, "DevWheel", message, ToolTipIcon.Warning);

    private void UpdateTooltip()
    {
        var text = _controller.IsPaused ? "DevWheel (paused)" : $"DevWheel: {_controller.HotkeyText}";
        _icon.Text = text.Length > 63 ? text[..63] : text;
        _pause.Checked = _controller.IsPaused;
    }

    private static void ShowAbout()
    {
        var version = typeof(TrayIcon).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        System.Windows.MessageBox.Show(
            $"DevWheel {version}\n\nLocal developer multi-tool. Works fully offline: no network access, " +
            "no logging of what you select or generate.",
            "About DevWheel", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>Draws a small wheel icon so the app ships without binary assets.</summary>
    private static Icon DrawIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var ring = new Pen(Color.FromArgb(0, 120, 212), 6);
            g.DrawEllipse(ring, 4, 4, 24, 24);
            using var spoke = new Pen(Color.FromArgb(0, 120, 212), 2);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Math.PI / 3;
                g.DrawLine(spoke, 16, 16, 16 + (float)(Math.Sin(a) * 11), 16 - (float)(Math.Cos(a) * 11));
            }
            using var hub = new SolidBrush(Color.FromArgb(0, 120, 212));
            g.FillEllipse(hub, 12, 12, 8, 8);
        }
        var handle = bmp.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            Interop.WindowHelper.DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _controller.HotkeyChanged -= UpdateTooltip;
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }
}
