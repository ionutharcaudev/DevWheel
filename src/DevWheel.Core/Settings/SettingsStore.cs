using System.Text.Json;

namespace DevWheel.Core;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON (normally %APPDATA%\DevWheel\settings.json).</summary>
public sealed class SettingsStore(string path)
{
    public string Path { get; } = path;

    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevWheel", "settings.json");

    public AppSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            if (File.Exists(Path))
            {
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), AppSettings.JsonOptions);
            }
        }
        catch (JsonException)
        {
            // A corrupt file falls back to defaults; keep a copy so the user's edits are not silently lost.
            TryBackupCorrupt();
        }
        catch (IOException)
        {
        }

        settings ??= new AppSettings();
        settings.Normalize();
        return settings;
    }

    public void Save(AppSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, AppSettings.JsonOptions));
        File.Move(tmp, Path, overwrite: true);
    }

    private void TryBackupCorrupt()
    {
        try
        {
            File.Copy(Path, Path + ".bad", overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
