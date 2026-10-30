namespace DevWheel.Core.Tests;

public class ToolRegistryTests
{
    private static readonly ToolRegistry Registry = ToolRegistry.CreateDefault(TimeProvider.System);

    [Fact]
    public void Has_the_v1_catalogue()
    {
        Assert.Equal(22, Registry.All.Count);
        Assert.Equal(ToolGroups.DefaultOrder, Registry.BuildWheel(new AppSettings()).Select(g => g.Name));
    }

    [Fact]
    public void Ids_are_unique() => Assert.Equal(Registry.All.Count, Registry.All.Select(t => t.Id).Distinct().Count());

    [Fact]
    public void Sensitive_tools_match_the_catalogue()
    {
        var sensitive = Registry.All.Where(t => t.IsSensitive).Select(t => t.Id).Order();
        Assert.Equal(
            ["aes.decrypt", "aes.encrypt", "generate.laravel", "generate.password", "generate.secret", "hash.hmac256", "jwt.decode"],
            sensitive);
    }

    [Fact]
    public void Wheel_follows_settings()
    {
        var settings = new AppSettings
        {
            GroupOrder = [ToolGroups.Crypto, ToolGroups.Encode],
            HiddenGroups = [ToolGroups.Inspect],
            HiddenTools = ["hash.md5", "hash.sha1"],
            ToolOrder = ["hash.hmac256"],
        };
        var wheel = Registry.BuildWheel(settings);
        Assert.Equal([ToolGroups.Crypto, ToolGroups.Encode, ToolGroups.Format, ToolGroups.Hash, ToolGroups.Generate], wheel.Select(g => g.Name));
        Assert.Equal(["hash.hmac256", "hash.sha256"], wheel.Single(g => g.Name == ToolGroups.Hash).Tools.Select(t => t.Id));
    }

    [Fact]
    public void Group_with_all_tools_hidden_is_dropped()
    {
        var settings = new AppSettings { HiddenTools = ["aes.encrypt", "aes.decrypt"] };
        Assert.DoesNotContain(Registry.BuildWheel(settings), g => g.Name == ToolGroups.Crypto);
    }
}

public class HotkeySettingTests
{
    [Fact]
    public void Default_is_ctrl_alt_space() => Assert.Equal("Ctrl+Alt+Space", HotkeySetting.Default.ToString());

    [Theory]
    [InlineData(HotkeySetting.ModShift, 0x41u, "Use at least one of Ctrl, Alt or Win")]
    [InlineData(HotkeySetting.ModControl, 0x11u, "Press a key together with the modifiers")]
    [InlineData(HotkeySetting.ModControl | HotkeySetting.ModAlt, 0x2Eu, "Ctrl+Alt+Del is reserved by Windows")]
    [InlineData(HotkeySetting.ModWin, 0x4Cu, "Win+L is reserved by Windows")]
    public void Rejects(uint modifiers, uint vk, string error) => Assert.Equal(error, new HotkeySetting(modifiers, vk).Validate());

    [Fact]
    public void Accepts_normal_combination() =>
        Assert.Null(new HotkeySetting(HotkeySetting.ModControl | HotkeySetting.ModAlt, 0x47).Validate());
}

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "devwheel-tests-" + Guid.NewGuid().ToString("N"));

    private string File => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var settings = new SettingsStore(File).Load();
        Assert.Equal(HotkeySetting.Default, settings.Hotkey);
        Assert.Equal(30, settings.ClipboardClearSeconds);
    }

    [Fact]
    public void Round_trips()
    {
        var store = new SettingsStore(File);
        var settings = new AppSettings
        {
            Hotkey = new HotkeySetting(HotkeySetting.ModControl | HotkeySetting.ModShift, 0x57),
            TriggerMode = TriggerMode.Hold,
            ClipboardClearSeconds = 45,
            ToolHotkeys = { ["generate.guid"] = new HotkeySetting(HotkeySetting.ModControl | HotkeySetting.ModAlt, 0x47) },
            OutputPreferences = { ["base64.encode"] = OutputPreference.ClipboardOnly },
            Generators = new GeneratorSettings { Secret = new SecretOptions(64, SecretFormat.Base64) },
        };
        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings.Hotkey, loaded.Hotkey);
        Assert.Equal(TriggerMode.Hold, loaded.TriggerMode);
        Assert.Equal(45, loaded.ClipboardClearSeconds);
        Assert.Equal(settings.ToolHotkeys["generate.guid"], loaded.ToolHotkeys["generate.guid"]);
        Assert.Equal(OutputPreference.ClipboardOnly, loaded.OutputPreferences["base64.encode"]);
        Assert.Equal(new SecretOptions(64, SecretFormat.Base64), loaded.Generators.Secret);
        Assert.Contains("\"TriggerMode\": \"Hold\"", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void Clear_delay_cannot_be_turned_off()
    {
        Directory.CreateDirectory(_dir);
        System.IO.File.WriteAllText(File, "{\"ClipboardClearSeconds\": 0}");
        Assert.Equal(AppSettings.MinClearSeconds, new SettingsStore(File).Load().ClipboardClearSeconds);
    }

    [Fact]
    public void Corrupt_file_falls_back_and_is_kept()
    {
        Directory.CreateDirectory(_dir);
        System.IO.File.WriteAllText(File, "{ not json");
        Assert.Equal(HotkeySetting.Default, new SettingsStore(File).Load().Hotkey);
        Assert.True(System.IO.File.Exists(File + ".bad"));
    }

    [Fact]
    public void Settings_have_no_place_for_secrets()
    {
        // SEC-03: only preferences are persisted. A new property must be added here on purpose, after review.
        string[] reviewed =
        [
            "ClipboardClearSeconds", "Generators", "GroupOrder", "HiddenGroups", "HiddenTools", "Hotkey",
            "OutputPreferences", "StartWithWindows", "ToolHotkeys", "ToolOrder", "TriggerMode",
        ];
        Assert.Equal(reviewed, typeof(AppSettings).GetProperties().Select(p => p.Name).Order());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }
}
