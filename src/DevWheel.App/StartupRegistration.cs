using System;
using Microsoft.Win32;

namespace DevWheel.App;

/// <summary>"Start with Windows" through the per-user Run key (no admin rights needed).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DevWheel";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled && Environment.ProcessPath is { } path) key.SetValue(ValueName, $"\"{path}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Toast.ShowError("Could not change \"Start with Windows\" (blocked by policy?)");
        }
    }
}
