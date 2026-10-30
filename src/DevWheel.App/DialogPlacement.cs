using System.Windows;
using System.Windows.Interop;
using DevWheel.Interop;

namespace DevWheel.App;

/// <summary>Opens dialogs next to the mouse cursor and brings them to the front.</summary>
public static class DialogPlacement
{
    public static void NearCursor(Window window)
    {
        var cursor = WindowHelper.GetCursorPos();
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            WindowHelper.PlaceNear(hwnd, cursor, -24, -24);
            WindowHelper.Activate(hwnd);
        };
    }
}
