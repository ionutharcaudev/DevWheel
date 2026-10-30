using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DevWheel.Core;

namespace DevWheel.App;

/// <summary>Settings: hotkey and trigger mode, wheel content and order, output, auto-clear, generators, startup.</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<uint, uint, bool> _isAvailable;
    private readonly ObservableCollection<GroupRow> _groups;
    private readonly ObservableCollection<ToolRow> _tools;
    private HotkeySetting _hotkey;

    // What the next key press is recorded for: null = nothing, MainTarget = the main hotkey, else a tool row.
    private object? _recording;
    private static readonly object MainTarget = new();

    public SettingsWindow(AppSettings settings, ToolRegistry registry, Func<uint, uint, bool> isAvailable)
    {
        InitializeComponent();
        _settings = settings;
        _isAvailable = isAvailable;
        _hotkey = settings.Hotkey;

        HotkeyText.Text = _hotkey.ToString();
        TapMode.IsChecked = settings.TriggerMode == TriggerMode.Tap;
        HoldMode.IsChecked = settings.TriggerMode == TriggerMode.Hold;
        ClearSecondsBox.Text = settings.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture);
        ClearRangeText.Text = $"seconds ({AppSettings.MinClearSeconds}-{AppSettings.MaxClearSeconds}; cannot be turned off)";
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;

        var groupOrder = settings.GroupOrder.Where(ToolGroups.DefaultOrder.Contains).Distinct()
            .Concat(ToolGroups.DefaultOrder.Where(g => !settings.GroupOrder.Contains(g)));
        _groups = new(groupOrder.Select(g => new GroupRow(g, !settings.HiddenGroups.Contains(g))));
        GroupList.ItemsSource = _groups;

        var ordered = registry.All.OrderBy(t =>
        {
            var i = settings.ToolOrder.IndexOf(t.Id);
            return i >= 0 ? i : settings.ToolOrder.Count + registry.All.ToList().IndexOf(t);
        });
        _tools = new(ordered.Select(t =>
        {
            var row = new ToolRow(t)
            {
                Visible = !settings.HiddenTools.Contains(t.Id),
                Hotkey = settings.ToolHotkeys.GetValueOrDefault(t.Id),
            };
            if (row.CanReplace && settings.OutputPreferences.GetValueOrDefault(t.Id) == OutputPreference.ClipboardOnly)
            {
                row.Output = ToolRow.ClipboardOnly;
            }
            return row;
        }));
        ToolList.ItemsSource = _tools;

        SecretBytesBox.ItemsSource = SecretOptions.AllowedBytes.Select(b => $"{b} bytes").ToList();
        SecretBytesBox.SelectedIndex = SecretOptions.AllowedBytes.ToList().IndexOf(settings.Generators.Secret.Bytes);
        SecretFormatBox.ItemsSource = Enum.GetNames<SecretFormat>();
        SecretFormatBox.SelectedItem = settings.Generators.Secret.Format.ToString();
        var password = settings.Generators.Password;
        PasswordLengthBox.Text = password.Length.ToString(CultureInfo.InvariantCulture);
        PasswordRangeText.Text = $"({PasswordOptions.MinLength}-{PasswordOptions.MaxLength})";
        UpperBox.IsChecked = password.Upper;
        LowerBox.IsChecked = password.Lower;
        DigitsBox.IsChecked = password.Digits;
        SymbolsBox.IsChecked = password.Symbols;

        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>The settings to apply, or null when the window was cancelled.</summary>
    public AppSettings? Saved { get; private set; }

    // ---------- hotkey recording ----------

    private void RecordMain_Click(object sender, RoutedEventArgs e) => StartRecording(MainTarget, "Press the new combination... (Esc cancels)");

    private void RecordTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToolRow row })
        {
            StartRecording(row, $"Press a hotkey for {row.Name}... (Backspace clears, Esc cancels)");
            row.HotkeyText = "Press keys...";
        }
    }

    private void StartRecording(object target, string status)
    {
        StopRecording();
        _recording = target;
        if (target == MainTarget) RecordButton.Content = "Recording...";
        HotkeyStatus.Text = status;
        ErrorText.Text = "";
    }

    private void StopRecording()
    {
        if (_recording is ToolRow row) row.Refresh();
        _recording = null;
        RecordButton.Content = "Record";
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        _hotkey = HotkeySetting.Default;
        HotkeyText.Text = _hotkey.ToString();
        HotkeyStatus.Text = _isAvailable(_hotkey.Modifiers, _hotkey.VirtualKey) ? "Default restored." : "Default restored, but it is taken by another app.";
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recording is null) return;
        e.Handled = true;

        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var modifiers = ToHotkeyModifiers(Keyboard.Modifiers);
        var report = _recording == MainTarget ? (Action<string>)(s => HotkeyStatus.Text = s) : s => ErrorText.Text = s;

        if (modifiers == 0 && key == Key.Escape)
        {
            HotkeyStatus.Text = "";
            StopRecording();
            return;
        }
        if (_recording is ToolRow clearRow && modifiers == 0 && key is Key.Back or Key.Delete)
        {
            clearRow.Hotkey = null;
            HotkeyStatus.Text = "";
            StopRecording();
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (HotkeySetting.IsModifierKey(vk)) return; // wait for the main key

        var candidate = new HotkeySetting(modifiers, vk);
        if (candidate.Validate() is { } error)
        {
            report(error);
            return;
        }
        if (IsUsedElsewhere(candidate, _recording))
        {
            report($"{candidate} is already used by DevWheel");
            return;
        }
        if (!_isAvailable(candidate.Modifiers, candidate.VirtualKey))
        {
            report($"{candidate} is taken by another app; try another combination");
            return;
        }

        if (_recording == MainTarget)
        {
            _hotkey = candidate;
            HotkeyText.Text = candidate.ToString();
            HotkeyStatus.Text = $"{candidate} is available.";
        }
        else if (_recording is ToolRow row)
        {
            row.Hotkey = candidate;
            HotkeyStatus.Text = "";
        }
        StopRecording();
    }

    private bool IsUsedElsewhere(HotkeySetting candidate, object target) =>
        (target != MainTarget && candidate == _hotkey) || _tools.Any(t => t != target && t.Hotkey == candidate);

    private static uint ToHotkeyModifiers(ModifierKeys keys) =>
        ((keys & ModifierKeys.Control) != 0 ? HotkeySetting.ModControl : 0)
        | ((keys & ModifierKeys.Alt) != 0 ? HotkeySetting.ModAlt : 0)
        | ((keys & ModifierKeys.Shift) != 0 ? HotkeySetting.ModShift : 0)
        | ((keys & ModifierKeys.Windows) != 0 ? HotkeySetting.ModWin : 0);

    // ---------- ordering ----------

    private void GroupUp_Click(object sender, RoutedEventArgs e) => Move(GroupList, _groups, -1);
    private void GroupDown_Click(object sender, RoutedEventArgs e) => Move(GroupList, _groups, +1);
    private void ToolUp_Click(object sender, RoutedEventArgs e) => Move(ToolList, _tools, -1);
    private void ToolDown_Click(object sender, RoutedEventArgs e) => Move(ToolList, _tools, +1);

    private static void Move<T>(Selector list, ObservableCollection<T> items, int step)
    {
        var index = list.SelectedIndex;
        var target = index + step;
        if (index < 0 || target < 0 || target >= items.Count) return;
        items.Move(index, target);
        list.SelectedIndex = target;
    }

    // ---------- save ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        if (!int.TryParse(ClearSecondsBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var clear)
            || clear < AppSettings.MinClearSeconds || clear > AppSettings.MaxClearSeconds)
        {
            ErrorText.Text = $"Auto-clear must be {AppSettings.MinClearSeconds} to {AppSettings.MaxClearSeconds} seconds";
            return;
        }
        if (!int.TryParse(PasswordLengthBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            ErrorText.Text = "Password length must be a number";
            return;
        }
        var password = new PasswordOptions(length, UpperBox.IsChecked == true, LowerBox.IsChecked == true,
            DigitsBox.IsChecked == true, SymbolsBox.IsChecked == true);
        if (password.Validate() is { } passwordError)
        {
            ErrorText.Text = passwordError;
            return;
        }

        var s = _settings;
        s.Hotkey = _hotkey;
        s.TriggerMode = HoldMode.IsChecked == true ? TriggerMode.Hold : TriggerMode.Tap;
        s.ClipboardClearSeconds = clear;
        s.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        s.GroupOrder = _groups.Select(g => g.Name).ToList();
        s.HiddenGroups = _groups.Where(g => !g.Visible).Select(g => g.Name).ToList();
        s.ToolOrder = _tools.Select(t => t.Id).ToList();
        s.HiddenTools = _tools.Where(t => !t.Visible).Select(t => t.Id).ToList();
        s.OutputPreferences = _tools.Where(t => t.CanReplace && t.Output == ToolRow.ClipboardOnly)
            .ToDictionary(t => t.Id, _ => OutputPreference.ClipboardOnly);
        s.ToolHotkeys = _tools.Where(t => t.Hotkey is not null).ToDictionary(t => t.Id, t => t.Hotkey!);
        s.Generators = new GeneratorSettings
        {
            Secret = new SecretOptions(
                SecretOptions.AllowedBytes[Math.Max(0, SecretBytesBox.SelectedIndex)],
                Enum.Parse<SecretFormat>((string)SecretFormatBox.SelectedItem)),
            Password = password,
        };
        s.Normalize();
        Saved = s;
        Close();
    }

    // ---------- rows ----------

    public sealed class GroupRow(string name, bool visible)
    {
        public string Name { get; } = name;
        public bool Visible { get; set; } = visible;
    }

    public sealed class ToolRow(IDevTool tool) : INotifyPropertyChanged
    {
        public const string Replace = "Replace selection";
        public const string ClipboardOnly = "Clipboard only";

        private HotkeySetting? _hotkey;
        private string? _hotkeyText;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; } = tool.Id;
        public string Group { get; } = tool.Group;
        public string Name { get; } = tool.Name;
        public bool Visible { get; set; } = true;
        public bool CanReplace { get; } = tool.DefaultOutput == OutputMode.ReplaceSelection;
        public IReadOnlyList<string> OutputChoices { get; } =
            tool.DefaultOutput == OutputMode.ReplaceSelection ? [Replace, ClipboardOnly] : [Describe(tool.DefaultOutput)];
        public string Output { get; set; } = Describe(tool.DefaultOutput);

        public HotkeySetting? Hotkey
        {
            get => _hotkey;
            set
            {
                _hotkey = value;
                Refresh();
            }
        }

        public string HotkeyText
        {
            get => _hotkeyText ?? _hotkey?.ToString() ?? "None";
            set
            {
                _hotkeyText = value;
                OnChanged();
            }
        }

        public void Refresh()
        {
            _hotkeyText = null;
            OnChanged(nameof(HotkeyText));
        }

        private static string Describe(OutputMode mode) => mode switch
        {
            OutputMode.ReplaceSelection => Replace,
            OutputMode.Clipboard => "Clipboard",
            _ => "Viewer",
        };

        private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
