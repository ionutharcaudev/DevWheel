using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DevWheel.Core;
using DevWheel.Interop;

namespace DevWheel.App;

public sealed record WheelChoice(IDevTool Tool, bool AskForOptions);

/// <summary>
/// Variant C: groups on an inner ring, the open group's tools in an outer arc. Drawn from code and hit-tested
/// with polar math: the distance from the center picks the zone, the angle picks the group or tool.
/// </summary>
public sealed class WheelWindow : Window
{
    // Window side in WPF units. Larger than the wheel so a fast flick past the arc does not leave the window.
    private const double Side = 560;
    private const double C = Side / 2;
    private const double HubRadius = 52;
    private const double InnerFrom = 56;
    private const double InnerTo = 128;
    private const double OuterFrom = 136;
    private const double OuterTo = 208;
    private const double ToolSweep = 24;
    private const double OuterGrace = 28;

    private static readonly TimeSpan HoldTapGrace = TimeSpan.FromMilliseconds(300);

    private static readonly Dictionary<string, string> GroupGlyphs = new()
    {
        [ToolGroups.Encode] = "",   // Code
        [ToolGroups.Inspect] = "",  // Search
        [ToolGroups.Format] = "",   // AlignLeft
        [ToolGroups.Hash] = "",     // Permissions
        [ToolGroups.Generate] = "", // LightningBolt
        [ToolGroups.Crypto] = "",   // Lock
    };

    private readonly IReadOnlyList<ToolGroup> _groups;
    private readonly string? _selection;
    private readonly Theme _theme;
    private readonly AppSettings _settings;
    private readonly uint _holdKey;
    private readonly TaskCompletionSource<WheelChoice?> _result = new();
    private readonly Canvas _root = new() { Width = Side, Height = Side };
    private readonly Canvas _arcLayer = new() { Width = Side, Height = Side, IsHitTestVisible = false };
    private readonly List<Path> _groupPaths = [];
    private readonly List<Path> _toolPaths = [];
    private readonly TextBlock _hubTitle;
    private readonly TextBlock _hubHint;
    private readonly TextBlock _hubExtra;
    private readonly Stopwatch _shown = new();
    private DispatcherTimer? _holdTimer;

    private int _group = -1;
    private int _tool = -1;
    private bool _pointerInRing;
    private bool _keyboardTools; // number keys and arrows act on the open group's tools
    private bool _keyboardActive; // last interaction was a key, not the pointer
    private Point? _lastPointer;
    private string? _flash;

    private WheelWindow(IReadOnlyList<ToolGroup> groups, string? selection, Theme theme, AppSettings settings)
    {
        _groups = groups;
        _selection = selection;
        _theme = theme;
        _settings = settings;
        _holdKey = settings.Hotkey.VirtualKey;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = Side;
        Height = Side;
        Left = -32000;
        Top = -32000;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        UseLayoutRounding = true;

        _root.Background = theme.HitTestFill;
        BuildRing();
        _root.Children.Add(_arcLayer);

        _hubTitle = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 12.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = theme.Foreground };
        _hubHint = new TextBlock { FontSize = 10.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = theme.SubtleForeground, Margin = new Thickness(0, 2, 0, 0) };
        _hubExtra = new TextBlock { FontSize = 10.5, TextAlignment = TextAlignment.Center, Foreground = theme.AccentBrush, Margin = new Thickness(0, 2, 0, 0) };
        BuildHub();

        Content = _root;
        UpdateVisuals();

        SourceInitialized += (_, _) => WindowHelper.SetToolWindowStyle(new WindowInteropHelper(this).Handle, noActivate: false, clickThrough: false);
        MouseMove += (_, e) => OnPointer(e.GetPosition(_root));
        MouseLeftButtonDown += (_, _) => OnClick();
        MouseRightButtonDown += (_, _) => Finish(null);
        MouseLeave += (_, _) =>
        {
            if (_settings.TriggerMode == TriggerMode.Tap) Finish(null);
        };
        Deactivated += (_, _) => Finish(null);
        PreviewKeyDown += OnKey;
    }

    private Task<WheelChoice?> Result => _result.Task;

    /// <summary>Shows the wheel centered on <paramref name="cursor"/> and waits for a choice (null = dismissed).</summary>
    public static Task<WheelChoice?> ShowAsync(POINT cursor, IReadOnlyList<ToolGroup> groups, string? selection, AppSettings settings)
    {
        if (groups.Count == 0)
        {
            Toast.ShowError("All tools are hidden; enable some in Settings", cursor);
            return Task.FromResult<WheelChoice?>(null);
        }

        var window = new WheelWindow(groups, selection, Theme.Current(), settings);
        if (Theme.AnimationsEnabled) window.Opacity = 0;
        window.Show();
        var hwnd = new WindowInteropHelper(window).Handle;
        WindowHelper.PlaceCentered(hwnd, cursor, Side, Side);
        WindowHelper.Activate(hwnd);
        window.Activate();
        window.Focus();
        Keyboard.Focus(window);
        if (Theme.AnimationsEnabled) window.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100)));
        window._shown.Start();
        if (settings.TriggerMode == TriggerMode.Hold) window.StartHoldPolling();
        return window.Result;
    }

    // ---------- construction ----------

    private double GroupSweep => 360.0 / _groups.Count;

    private void BuildRing()
    {
        for (var i = 0; i < _groups.Count; i++)
        {
            var center = i * GroupSweep;
            var path = new Path
            {
                Data = Sector(InnerFrom, InnerTo, center - GroupSweep / 2, center + GroupSweep / 2),
                StrokeThickness = 1,
                Stroke = _theme.Separator,
            };
            _groupPaths.Add(path);
            _root.Children.Add(path);

            var group = _groups[i];
            var label = new StackPanel { IsHitTestVisible = false };
            label.Children.Add(new TextBlock
            {
                Text = GroupGlyphs.GetValueOrDefault(group.Name, ""),
                FontFamily = Theme.IconFont,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = _theme.Foreground,
            });
            label.Children.Add(new TextBlock
            {
                Text = group.Name,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = _theme.Foreground,
            });
            AddCentered(_root, label, Polar((InnerFrom + InnerTo) / 2, center), 80, 44);
        }
    }

    private void BuildHub()
    {
        _root.Children.Add(new Ellipse
        {
            Width = HubRadius * 2,
            Height = HubRadius * 2,
            Fill = _theme.HubBackground,
            Stroke = _theme.Separator,
            StrokeThickness = 1,
            IsHitTestVisible = false,
        });
        Canvas.SetLeft(_root.Children[^1], C - HubRadius);
        Canvas.SetTop(_root.Children[^1], C - HubRadius);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        text.Children.Add(_hubTitle);
        text.Children.Add(_hubHint);
        text.Children.Add(_hubExtra);
        AddCentered(_root, text, new Point(C, C), HubRadius * 2 - 12, HubRadius * 2 - 8);
    }

    private void BuildArc()
    {
        _arcLayer.Children.Clear();
        _toolPaths.Clear();
        if (_group < 0) return;

        var tools = _groups[_group].Tools;
        var start = ArcStart(_group);
        for (var j = 0; j < tools.Count; j++)
        {
            var tool = tools[j];
            var from = start + j * ToolSweep;
            var path = new Path
            {
                Data = Sector(OuterFrom, OuterTo, from, from + ToolSweep),
                Opacity = IsUsable(tool) ? 1 : 0.35,
            };
            _toolPaths.Add(path);
            _arcLayer.Children.Add(path);

            var mid = from + ToolSweep / 2;
            var label = new TextBlock
            {
                Text = tool.Label,
                FontSize = 10.5,
                TextAlignment = TextAlignment.Center,
                Foreground = _theme.Foreground,
                Opacity = IsUsable(tool) ? 1 : 0.45,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AddCentered(_arcLayer, label, Polar((OuterFrom + OuterTo) / 2 - 4, mid), 60, 36);

            if (tool.IsSensitive)
            {
                AddCentered(_arcLayer, new TextBlock
                {
                    Text = "",
                    FontFamily = Theme.IconFont,
                    FontSize = 9,
                    Foreground = _theme.SubtleForeground,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                }, Polar(OuterTo - 10, mid), 14, 14);
            }
        }

        if (Theme.AnimationsEnabled)
        {
            _arcLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        }
    }

    // ---------- interaction ----------

    private void OnPointer(Point p)
    {
        // WPF also raises MouseMove when visuals change under a still pointer; only real movement counts,
        // so keyboard navigation is not undone by the arc appearing under the cursor.
        if (_lastPointer is { } last && Math.Abs(last.X - p.X) < 0.5 && Math.Abs(last.Y - p.Y) < 0.5) return;
        _lastPointer = p;
        _keyboardTools = false;
        _keyboardActive = false;

        var dx = p.X - C;
        var dy = p.Y - C;
        var radius = Math.Sqrt(dx * dx + dy * dy);
        var angle = Normalize(Math.Atan2(dx, -dy) * 180 / Math.PI);
        _flash = null;
        _pointerInRing = radius >= InnerFrom && radius < OuterFrom;

        if (_pointerInRing)
        {
            SetState(GroupAt(angle), -1);
        }
        else if (radius >= OuterFrom && radius <= OuterTo + OuterGrace && _group >= 0)
        {
            SetState(_group, ToolAt(_group, angle));
        }
        else
        {
            SetState(_group, -1);
        }
    }

    private void OnClick()
    {
        if (_tool >= 0)
        {
            Choose(_groups[_group].Tools[_tool]);
        }
        else if (_pointerInRing && _group >= 0 && _groups[_group].Tools.Count == 1)
        {
            Choose(_groups[_group].Tools[0]);
        }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;
        _flash = null;
        _keyboardActive = true;

        var number = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
            _ => 0,
        };
        if (number > 0)
        {
            if (!_keyboardTools)
            {
                if (number > _groups.Count) return;
                if (_groups[number - 1].Tools.Count == 1)
                {
                    Choose(_groups[number - 1].Tools[0]);
                    return;
                }
                _keyboardTools = true;
                SetState(number - 1, -1);
            }
            else if (_group >= 0 && number <= _groups[_group].Tools.Count)
            {
                Choose(_groups[_group].Tools[number - 1]);
            }
            return;
        }

        switch (key)
        {
            case Key.Escape:
                Finish(null);
                break;
            case Key.Right or Key.Down:
                Rotate(+1);
                break;
            case Key.Left or Key.Up:
                Rotate(-1);
                break;
            case Key.Back:
                _keyboardTools = false;
                SetState(_group, -1);
                break;
            case Key.Enter:
                if (_tool >= 0)
                {
                    Choose(_groups[_group].Tools[_tool]);
                }
                else if (_group >= 0)
                {
                    if (_groups[_group].Tools.Count == 1)
                    {
                        Choose(_groups[_group].Tools[0]);
                        return;
                    }
                    _keyboardTools = true;
                    SetState(_group, 0);
                }
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    private void Rotate(int step)
    {
        if (_keyboardTools && _group >= 0)
        {
            var count = _groups[_group].Tools.Count;
            SetState(_group, _tool < 0 ? (step > 0 ? 0 : count - 1) : (_tool + step + count) % count);
        }
        else
        {
            var count = _groups.Count;
            SetState(_group < 0 ? (step > 0 ? 0 : count - 1) : (_group + step + count) % count, -1);
        }
    }

    private void StartHoldPolling()
    {
        _holdTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
        _holdTimer.Tick += (_, _) =>
        {
            if (InputSimulator.IsKeyDown((int)_holdKey)) return;
            _holdTimer.Stop();
            if (_tool >= 0)
            {
                Choose(_groups[_group].Tools[_tool], dismissIfDisabled: true);
            }
            else if (_shown.Elapsed >= HoldTapGrace)
            {
                Finish(null);
            }
            // A release right after opening means the user tapped: stay open and work like tap mode.
        };
        _holdTimer.Start();
    }

    private void Choose(IDevTool tool, bool dismissIfDisabled = false)
    {
        if (!IsUsable(tool))
        {
            if (dismissIfDisabled)
            {
                Finish(null);
                return;
            }
            _flash = "Needs selected text";
            UpdateVisuals();
            return;
        }
        Finish(new WheelChoice(tool, (Keyboard.Modifiers & ModifierKeys.Shift) != 0));
    }

    private void Finish(WheelChoice? choice)
    {
        if (!_result.TrySetResult(choice)) return;
        _holdTimer?.Stop();
        Hide();
        Dispatcher.BeginInvoke(Close);
    }

    // ---------- state and visuals ----------

    private void SetState(int group, int tool)
    {
        if (group != _group)
        {
            _group = group;
            BuildArc();
        }
        _tool = tool;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        for (var i = 0; i < _groupPaths.Count; i++)
        {
            _groupPaths[i].Fill = i == _group ? _theme.GroupHover : _theme.RingBackground;
        }
        for (var j = 0; j < _toolPaths.Count; j++)
        {
            var hovered = j == _tool;
            _toolPaths[j].Fill = hovered ? _theme.ToolHover : _theme.RingBackground;
            _toolPaths[j].Stroke = hovered ? _theme.AccentBrush : _theme.Separator;
            _toolPaths[j].StrokeThickness = hovered ? 2 : 1;
        }

        string title, hint, extra = "";
        if (_tool >= 0)
        {
            var tool = _groups[_group].Tools[_tool];
            title = tool.Name;
            hint = _flash ?? (IsUsable(tool) ? tool.Hint : "Needs selected text");
            if (tool.IsSensitive) extra = $"clears in {_settings.ClipboardClearSeconds} s";
        }
        else if (_group >= 0 && (_pointerInRing || _keyboardActive))
        {
            var group = _groups[_group];
            title = group.Name;
            hint = _flash ?? (group.Tools.Count == 1 ? group.Tools[0].Name : $"{group.Tools.Count} tools");
        }
        else
        {
            title = _selection is null ? "No selection" : "Selection";
            hint = _flash ?? (_selection is null ? "Generators still work" : $"{_selection.Length} chars");
        }
        _hubTitle.Text = title;
        _hubHint.Text = hint;
        _hubExtra.Text = extra;
        _hubExtra.Visibility = extra.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool IsUsable(IDevTool tool) => !tool.RequiresSelection || _selection is not null;

    // ---------- geometry ----------

    private double ArcStart(int group) => group * GroupSweep - _groups[group].Tools.Count * ToolSweep / 2;

    private int GroupAt(double angle) => (int)(Normalize(angle + GroupSweep / 2) / GroupSweep) % _groups.Count;

    private int ToolAt(int group, double angle)
    {
        var offset = Normalize(angle - ArcStart(group));
        var index = (int)(offset / ToolSweep);
        return index < _groups[group].Tools.Count ? index : -1;
    }

    private static double Normalize(double degrees) => ((degrees % 360) + 360) % 360;

    /// <summary>Point at <paramref name="radius"/>, <paramref name="degrees"/> clockwise from the top.</summary>
    private static Point Polar(double radius, double degrees)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(C + radius * Math.Sin(rad), C - radius * Math.Cos(rad));
    }

    private static Geometry Sector(double r1, double r2, double from, double to)
    {
        if (to - from >= 360) to = from + 359.99;
        var large = to - from > 180;
        var figure = new PathFigure { StartPoint = Polar(r2, from), IsClosed = true };
        figure.Segments.Add(new ArcSegment(Polar(r2, to), new Size(r2, r2), 0, large, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(Polar(r1, to), true));
        figure.Segments.Add(new ArcSegment(Polar(r1, from), new Size(r1, r1), 0, large, SweepDirection.Counterclockwise, true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    private static void AddCentered(Canvas canvas, FrameworkElement element, Point center, double width, double height)
    {
        var host = new Grid { Width = width, Height = height, IsHitTestVisible = false };
        element.HorizontalAlignment = HorizontalAlignment.Center;
        element.VerticalAlignment = VerticalAlignment.Center;
        host.Children.Add(element);
        Canvas.SetLeft(host, center.X - width / 2);
        Canvas.SetTop(host, center.Y - height / 2);
        canvas.Children.Add(host);
    }
}
