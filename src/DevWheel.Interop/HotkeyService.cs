using System.Windows.Interop;

namespace DevWheel.Interop;

/// <summary>
/// Global hotkeys through RegisterHotKey on a hidden message-only HwndSource. No keyboard hooks are used.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private static readonly nint HwndMessage = -3;

    private readonly HwndSource _source;
    private readonly HashSet<int> _registered = [];

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("DevWheel.Hotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);
    }

    /// <summary>Raised on the UI thread with the id passed to <see cref="Register"/>.</summary>
    public event EventHandler<int>? Pressed;

    /// <summary>Window handle, also used as the clipboard owner.</summary>
    public nint Handle => _source.Handle;

    /// <summary>Registers (or re-registers) a hotkey. Returns false when another app already owns the combination.</summary>
    public bool Register(int id, uint modifiers, uint virtualKey)
    {
        Unregister(id);
        if (!NativeMethods.RegisterHotKey(Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey)) return false;
        _registered.Add(id);
        return true;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id)) NativeMethods.UnregisterHotKey(Handle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.ToList()) Unregister(id);
    }

    /// <summary>True when the combination can be registered right now (it is registered and released again).</summary>
    public bool IsAvailable(uint modifiers, uint virtualKey)
    {
        const int probeId = 0xBFFF;
        if (!NativeMethods.RegisterHotKey(Handle, probeId, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey)) return false;
        NativeMethods.UnregisterHotKey(Handle, probeId);
        return true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            handled = true;
            Pressed?.Invoke(this, (int)wParam);
        }
        return 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
