using System.Runtime.InteropServices;

namespace DevWheel.Interop;

/// <summary>
/// Sends Ctrl+C and Ctrl+V with SendInput. Only called right after the user presses the hotkey, and only
/// towards the window that was in front (see docs/security.md).
/// </summary>
public static class InputSimulator
{
    private const ushort VkControl = 0x11;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRShift = 0xA1;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private const ushort VkLMenu = 0xA4;
    private const ushort VkRMenu = 0xA5;
    private const ushort VkLWin = 0x5B;
    private const ushort VkRWin = 0x5C;
    private const ushort VkC = 0x43;
    private const ushort VkV = 0x56;

    // Unassigned virtual key. Tapping it before releasing Alt or Win stops the menu bar or Start menu from opening.
    private const ushort VkNoOp = 0xE8;

    private static readonly ushort[] Modifiers = [VkLShift, VkRShift, VkLControl, VkRControl, VkLMenu, VkRMenu, VkLWin, VkRWin];

    public static bool IsKeyDown(int virtualKey) => (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public static bool AnyModifierDown() => Array.Exists(Modifiers, vk => IsKeyDown(vk));

    /// <summary>
    /// Sends key-up for every modifier the user still holds, so that Ctrl+C is not seen as Ctrl+Alt+C.
    /// </summary>
    public static void ReleaseModifiers()
    {
        var inputs = new List<INPUT>();
        var held = Modifiers.Where(vk => IsKeyDown(vk)).ToList();
        if (held.Count == 0) return;
        if (held.Exists(vk => vk is VkLMenu or VkRMenu or VkLWin or VkRWin))
        {
            inputs.Add(Key(VkNoOp, up: false));
            inputs.Add(Key(VkNoOp, up: true));
        }
        inputs.AddRange(held.Select(vk => Key(vk, up: true)));
        Send(inputs);
    }

    public static void SendCopy() => SendCtrlChord(VkC);

    public static void SendPaste() => SendCtrlChord(VkV);

    /// <summary>Taps the no-op key; used to satisfy the foreground-lock rule before SetForegroundWindow.</summary>
    public static void TapNoOp() => Send([Key(VkNoOp, up: false), Key(VkNoOp, up: true)]);

    private static void SendCtrlChord(ushort vk)
    {
        ReleaseModifiers();
        Send([Key(VkControl, up: false), Key(vk, up: false), Key(vk, up: true), Key(VkControl, up: true)]);
    }

    private static INPUT Key(ushort vk, bool up)
    {
        var flags = up ? NativeMethods.KEYEVENTF_KEYUP : 0;
        if (vk is VkRControl or VkRMenu or VkLWin or VkRWin) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)NativeMethods.MapVirtualKeyW(vk, 0),
                    dwFlags = flags,
                },
            },
        };
    }

    private static void Send(IReadOnlyCollection<INPUT> inputs)
    {
        var array = inputs.ToArray();
        NativeMethods.SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
    }
}
