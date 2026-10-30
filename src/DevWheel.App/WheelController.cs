using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DevWheel.Core;
using DevWheel.Interop;

namespace DevWheel.App;

/// <summary>
/// The runtime pipeline: hotkey, remember target, capture selection, show wheel, run tool, deliver, clean up.
/// </summary>
public sealed class WheelController : IDisposable
{
    private const int MainHotkeyId = 1;
    private const int FirstToolHotkeyId = 100;

    private readonly ToolRegistry _registry;
    private readonly SettingsStore _store;
    private readonly HotkeyService _hotkeys;
    private readonly ClipboardService _clipboard;
    private readonly SelectionService _selection;
    private readonly PromptService _prompts = new();
    private readonly Dictionary<int, string> _toolHotkeyIds = [];

    private AppSettings _settings;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _clearTimer;
    private uint _pendingClear;
    private bool _busy;

    public WheelController(ToolRegistry registry, SettingsStore store, AppSettings settings, HotkeyService hotkeys,
        ClipboardService clipboard, SelectionService selection)
    {
        _registry = registry;
        _store = store;
        _settings = settings;
        _hotkeys = hotkeys;
        _clipboard = clipboard;
        _selection = selection;
    }

    /// <summary>Shows a tray balloon (set by the App once the tray icon exists).</summary>
    public Action<string>? Notify { get; set; }

    public event Action? HotkeyChanged;

    public bool IsPaused { get; private set; }

    public string HotkeyText => _settings.Hotkey.ToString();

    public void Start()
    {
        _hotkeys.Pressed += OnHotkey;
        if (!RegisterAll())
        {
            Notify?.Invoke($"{_settings.Hotkey} is taken by another app. Choose another hotkey in Settings.");
            OpenSettings();
        }
    }

    public void SetPaused(bool paused)
    {
        if (paused == IsPaused) return;
        IsPaused = paused;
        if (paused) _hotkeys.UnregisterAll();
        else if (_settingsWindow is null) RegisterAll();
        HotkeyChanged?.Invoke();
    }

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // Hotkeys are released while Settings is open so the recorder can capture them.
        _hotkeys.UnregisterAll();
        _settingsWindow = new SettingsWindow(_settings.Clone(), _registry, _hotkeys.IsAvailable);
        _settingsWindow.Closed += (_, _) =>
        {
            var saved = _settingsWindow.Saved;
            _settingsWindow = null;
            if (saved is not null) Apply(saved);
            else if (!IsPaused) RegisterAll();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void Apply(AppSettings next)
    {
        var previous = _settings;
        _settings = next;
        if (!IsPaused && !RegisterAll())
        {
            // Keep the old hotkey if the new one could not be registered.
            MessageBox.Show($"{next.Hotkey} is taken by another app. Keeping {previous.Hotkey}.", "DevWheel",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            _settings.Hotkey = previous.Hotkey;
            RegisterAll();
        }

        try
        {
            _store.Save(_settings);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Settings could not be saved: " + e.Message, "DevWheel", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        StartupRegistration.Apply(_settings.StartWithWindows);
        HotkeyChanged?.Invoke();
    }

    /// <summary>Registers the main hotkey and the per-tool hotkeys. Returns false when the main one is taken.</summary>
    private bool RegisterAll()
    {
        _hotkeys.UnregisterAll();
        _toolHotkeyIds.Clear();
        var mainOk = _hotkeys.Register(MainHotkeyId, _settings.Hotkey.Modifiers, _settings.Hotkey.VirtualKey);

        var failed = new List<string>();
        var id = FirstToolHotkeyId;
        foreach (var (toolId, hotkey) in _settings.ToolHotkeys)
        {
            if (_registry.Find(toolId) is not { } tool) continue;
            if (_hotkeys.Register(id, hotkey.Modifiers, hotkey.VirtualKey)) _toolHotkeyIds[id++] = toolId;
            else failed.Add($"{hotkey} ({tool.Name})");
        }
        if (failed.Count > 0) Notify?.Invoke("These hotkeys are taken by another app: " + string.Join(", ", failed));
        return mainOk;
    }

    private async void OnHotkey(object? sender, int id)
    {
        if (_busy || IsPaused) return;
        _busy = true;
        try
        {
            await HandleHotkeyAsync(id);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Toast.ShowError($"Something went wrong ({e.GetType().Name})");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task HandleHotkeyAsync(int id)
    {
        var target = WindowHelper.GetForeground();
        var cursor = WindowHelper.GetCursorPos();

        IDevTool? direct = null;
        if (id != MainHotkeyId)
        {
            if (!_toolHotkeyIds.TryGetValue(id, out var toolId) || _registry.Find(toolId) is not { } tool) return;
            direct = tool;
        }

        var capture = new CaptureResult(CaptureStatus.NoSelection, null);
        if (direct is null || direct.RequiresSelection)
        {
            capture = await _selection.CaptureAsync(target);
            if (capture.Status == CaptureStatus.BlockedByElevation) Toast.ShowError("Can't read selection from an admin window", cursor);
            else if (capture.Status == CaptureStatus.ClipboardBusy) Toast.ShowError("The clipboard is busy; try again", cursor);
        }

        var choice = direct is null
            ? await WheelWindow.ShowAsync(cursor, _registry.BuildWheel(_settings), capture.Text, _settings)
            : new WheelChoice(direct, false);
        if (choice is null) return;

        await RunToolAsync(choice, capture.Text, target, cursor);
    }

    private async Task RunToolAsync(WheelChoice choice, string? selection, nint target, POINT cursor)
    {
        var tool = choice.Tool;
        var context = new ToolContext(selection, _prompts, new ToolOptions(_settings.Generators, choice.AskForOptions));
        ToolResult result;
        try
        {
            result = await tool.RunAsync(context, CancellationToken.None);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Only the exception type: messages can echo input (SEC-02).
            result = new ToolResult.Failure($"{tool.Name} failed ({e.GetType().Name})");
        }

        switch (result)
        {
            case ToolResult.Success success:
                await DeliverAsync(tool, success, selection, target, cursor);
                break;
            case ToolResult.View view:
                if (view.CopyText is not null && TryCopy(view.CopyText, tool.IsSensitive, cursor))
                {
                    Toast.Show(view.Message, cursor);
                }
                ViewerWindow.Open(view.Title, view.Content, text => CopyFromViewer(text, tool.IsSensitive));
                break;
            case ToolResult.Failure failure:
                Toast.ShowError(failure.Message, cursor);
                break;
        }
    }

    private async Task DeliverAsync(IDevTool tool, ToolResult.Success success, string? selection, nint target, POINT cursor)
    {
        var sensitive = tool.IsSensitive;
        var replace = tool.DefaultOutput == OutputMode.ReplaceSelection
            && selection is not null
            && _settings.OutputPreferences.GetValueOrDefault(tool.Id) != OutputPreference.ClipboardOnly
            && WindowHelper.IsWindow(target);

        var message = success.Message;
        uint written;
        try
        {
            if (replace)
            {
                try
                {
                    written = await _selection.ReplaceAsync(target, success.Output, sensitive);
                }
                catch (InvalidOperationException)
                {
                    written = _clipboard.SetText(success.Output, sensitive);
                    message += " (could not paste; copied instead)";
                    replace = false;
                }
            }
            else
            {
                written = _clipboard.SetText(success.Output, sensitive);
                if (tool.DefaultOutput == OutputMode.ReplaceSelection) message += " (copied)";
            }
        }
        catch (ClipboardBusyException)
        {
            Toast.ShowError("The clipboard is busy; try again", cursor);
            return;
        }

        if (sensitive)
        {
            ScheduleClear(written);
            message += replace
                ? $". Clipboard clears in {_settings.ClipboardClearSeconds} s"
                : $". Copied, clears in {_settings.ClipboardClearSeconds} s";
        }
        Toast.Show(message, cursor);
    }

    private void CopyFromViewer(string text, bool sensitive)
    {
        if (TryCopy(text, sensitive, null))
        {
            Toast.Show(sensitive ? $"Copied, clears in {_settings.ClipboardClearSeconds} s" : "Copied");
        }
    }

    private bool TryCopy(string text, bool sensitive, POINT? cursor)
    {
        try
        {
            var written = _clipboard.SetText(text, sensitive);
            if (sensitive) ScheduleClear(written);
            return true;
        }
        catch (ClipboardBusyException)
        {
            Toast.ShowError("The clipboard is busy; try again", cursor);
            return false;
        }
    }

    /// <summary>Clears sensitive output after the configured delay unless the user copied something since (SEC-05).</summary>
    private void ScheduleClear(uint sequenceNumber)
    {
        _clearTimer?.Stop();
        _pendingClear = sequenceNumber;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.ClipboardClearSeconds) };
        timer.Tick += (_, _) =>
        {
            try
            {
                _clipboard.ClearIfUnchanged(_pendingClear);
                timer.Stop();
                if (_clearTimer == timer) _clearTimer = null;
            }
            catch (ClipboardBusyException)
            {
                timer.Interval = TimeSpan.FromSeconds(1); // try again shortly
            }
        };
        _clearTimer = timer;
        timer.Start();
    }

    public void Dispose()
    {
        _hotkeys.Pressed -= OnHotkey;
        if (_clearTimer is not null)
        {
            // Exiting must not leave a secret behind on the clipboard.
            _clearTimer.Stop();
            try
            {
                _clipboard.ClearIfUnchanged(_pendingClear);
            }
            catch (ClipboardBusyException)
            {
            }
        }
        _settingsWindow?.Close();
    }
}
