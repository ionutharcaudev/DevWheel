using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevWheel.Core;

public enum TriggerMode
{
    Tap,
    Hold,
}

public enum OutputPreference
{
    ReplaceSelection,
    ClipboardOnly,
}

/// <summary>User preferences only. Never holds keys, passphrases, selections or tool output (SEC-03).</summary>
public sealed class AppSettings
{
    public const int MinClearSeconds = 5;
    public const int MaxClearSeconds = 300;

    public HotkeySetting Hotkey { get; set; } = HotkeySetting.Default;
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Tap;
    public List<string> GroupOrder { get; set; } = [.. ToolGroups.DefaultOrder];
    public List<string> HiddenGroups { get; set; } = [];
    public List<string> ToolOrder { get; set; } = [];
    public List<string> HiddenTools { get; set; } = [];
    public Dictionary<string, OutputPreference> OutputPreferences { get; set; } = [];
    public Dictionary<string, HotkeySetting> ToolHotkeys { get; set; } = [];

    /// <summary>Auto-clear delay for sensitive output. Cannot be turned off (SEC-05).</summary>
    public int ClipboardClearSeconds { get; set; } = 30;

    public GeneratorSettings Generators { get; set; } = new();
    public bool StartWithWindows { get; set; }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;

    /// <summary>Repairs values a hand-edited file could break. Null checks guard against JSON nulls.</summary>
    public void Normalize()
    {
        if (Hotkey is null || Hotkey.Validate() is not null) Hotkey = HotkeySetting.Default;
        GroupOrder ??= [.. ToolGroups.DefaultOrder];
        HiddenGroups ??= [];
        ToolOrder ??= [];
        HiddenTools ??= [];
        OutputPreferences ??= [];
        ToolHotkeys ??= [];
        foreach (var (id, hk) in ToolHotkeys.ToList())
        {
            if (hk is null || hk.Validate() is not null || hk == Hotkey) ToolHotkeys.Remove(id);
        }
        ClipboardClearSeconds = Math.Clamp(ClipboardClearSeconds, MinClearSeconds, MaxClearSeconds);
        Generators ??= new GeneratorSettings();
        if (Generators.Secret is null || Generators.Secret.Validate() is not null)
            Generators = Generators with { Secret = new SecretOptions() };
        if (Generators.Password is null || Generators.Password.Validate() is not null)
            Generators = Generators with { Password = new PasswordOptions() };
    }
}
