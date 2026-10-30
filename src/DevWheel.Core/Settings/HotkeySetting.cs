namespace DevWheel.Core;

/// <summary>A global hotkey as RegisterHotKey modifiers plus a virtual-key code.</summary>
public sealed record HotkeySetting(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x1;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;
    public const uint ModWin = 0x8;

    private const uint VkSpace = 0x20;

    public static HotkeySetting Default { get; } = new(ModControl | ModAlt, VkSpace);

    // Combinations Windows reserves or that would break normal use.
    private static readonly HotkeySetting[] Reserved =
    [
        new(ModControl | ModAlt, 0x2E),   // Ctrl+Alt+Del
        new(ModWin, 0x4C),                // Win+L
        new(ModControl | ModShift, 0x1B), // Ctrl+Shift+Esc
        new(ModControl, 0x1B),            // Ctrl+Esc
        new(ModAlt, 0x09),                // Alt+Tab
        new(ModAlt | ModShift, 0x09),     // Alt+Shift+Tab
        new(ModAlt, 0x1B),                // Alt+Esc
        new(ModAlt, 0x73),                // Alt+F4
    ];

    /// <summary>Returns a user-facing error, or null when the combination is acceptable.</summary>
    public string? Validate()
    {
        if ((Modifiers & (ModControl | ModAlt | ModWin)) == 0) return "Use at least one of Ctrl, Alt or Win";
        if (VirtualKey == 0 || IsModifierKey(VirtualKey)) return "Press a key together with the modifiers";
        if (Array.Exists(Reserved, r => r == this)) return $"{this} is reserved by Windows";
        return null;
    }

    public static bool IsModifierKey(uint vk) =>
        vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & ModShift) != 0) parts.Add("Shift");
        if ((Modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    public static string KeyName(uint vk) => vk switch
    {
        (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) => ((char)vk).ToString(),
        >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "PrintScreen",
        0x2D => "Insert",
        0x2E => "Del",
        0x6A => "Num*",
        0x6B => "Num+",
        0x6D => "Num-",
        0x6E => "Num.",
        0x6F => "Num/",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"0x{vk:X2}",
    };
}
