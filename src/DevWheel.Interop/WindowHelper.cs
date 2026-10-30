namespace DevWheel.Interop;

/// <summary>Foreground window, cursor, elevation check and DPI-aware placement.</summary>
public static class WindowHelper
{
    public static nint GetForeground() => NativeMethods.GetForegroundWindow();

    public static bool IsWindow(nint hwnd) => hwnd != 0 && NativeMethods.IsWindow(hwnd);

    public static void DestroyIcon(nint icon) => NativeMethods.DestroyIcon(icon);

    public static POINT GetCursorPos() => NativeMethods.GetCursorPos(out var p) ? p : default;

    /// <summary>
    /// Brings <paramref name="hwnd"/> to the front. When the foreground lock refuses a plain
    /// SetForegroundWindow, attaches to the current foreground thread's input for the call.
    /// </summary>
    public static bool Activate(nint hwnd)
    {
        if (!IsWindow(hwnd)) return false;
        if (NativeMethods.SetForegroundWindow(hwnd) && NativeMethods.GetForegroundWindow() == hwnd) return true;

        var current = NativeMethods.GetCurrentThreadId();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var attached = foregroundThread != 0 && foregroundThread != current
            && NativeMethods.AttachThreadInput(current, foregroundThread, true);
        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) NativeMethods.AttachThreadInput(current, foregroundThread, false);
        }
        return NativeMethods.GetForegroundWindow() == hwnd;
    }

    /// <summary>
    /// True when the window belongs to an elevated process (or one we may not inspect) while we are not
    /// elevated. Windows (UIPI) then silently drops our simulated keystrokes.
    /// </summary>
    public static bool IsBlockedByUipi(nint hwnd)
    {
        if (IsProcessElevated(NativeMethods.GetCurrentProcess(), out var selfElevated) && selfElevated) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0) return false;
        var process = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return true;
        try
        {
            return !IsProcessElevated(process, out var elevated) || elevated;
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    private static bool IsProcessElevated(nint process, out bool elevated)
    {
        elevated = false;
        if (!NativeMethods.OpenProcessToken(process, NativeMethods.TOKEN_QUERY, out var token)) return false;
        try
        {
            if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, out var value, 4, out _)) return false;
            elevated = value != 0;
            return true;
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    /// <summary>Scale factor (1.0 = 96 DPI) of the monitor nearest to a physical point.</summary>
    public static double GetScaleAt(POINT point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 && dpi > 0
            ? dpi / 96.0
            : 1.0;
    }

    /// <summary>Work area, in physical pixels, of the monitor nearest to a point.</summary>
    public static RECT GetWorkArea(POINT point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        return NativeMethods.GetMonitorInfo(monitor, ref info) ? info.rcWork : new RECT { Right = 1920, Bottom = 1080 };
    }

    /// <summary>
    /// Places a window of <paramref name="dipWidth"/> x <paramref name="dipHeight"/> (WPF units) centered on a
    /// physical point, shifted so it stays inside that monitor's work area. Returns the physical rectangle.
    /// </summary>
    public static RECT PlaceCentered(nint hwnd, POINT center, double dipWidth, double dipHeight)
    {
        var scale = GetScaleAt(center);
        var width = (int)Math.Round(dipWidth * scale);
        var height = (int)Math.Round(dipHeight * scale);
        return Place(hwnd, center.X - width / 2, center.Y - height / 2, width, height, center);
    }

    /// <summary>Moves a window (keeping its current size) so its top-left is near a point, inside the work area.</summary>
    public static void PlaceNear(nint hwnd, POINT anchor, int offsetX, int offsetY)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
        var scale = GetScaleAt(anchor);
        Place(hwnd, anchor.X + (int)(offsetX * scale), anchor.Y + (int)(offsetY * scale), rect.Width, rect.Height, anchor);
    }

    private static RECT Place(nint hwnd, int x, int y, int width, int height, POINT monitorPoint)
    {
        var work = GetWorkArea(monitorPoint);
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));
        const uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
        // Twice: the first move may cross monitors, and WPF then resizes the window for the new DPI.
        NativeMethods.SetWindowPos(hwnd, 0, x, y, width, height, flags);
        NativeMethods.SetWindowPos(hwnd, 0, x, y, width, height, flags);
        return new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }

    /// <summary>Hides a window from Alt+Tab; optionally makes it non-activating and click-through (toasts).</summary>
    public static void SetToolWindowStyle(nint hwnd, bool noActivate, bool clickThrough)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE) | NativeMethods.WS_EX_TOOLWINDOW;
        if (noActivate) style |= NativeMethods.WS_EX_NOACTIVATE;
        if (clickThrough) style |= NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, (nint)style);
    }
}
