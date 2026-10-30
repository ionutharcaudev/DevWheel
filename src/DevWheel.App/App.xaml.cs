using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DevWheel.Core;
using DevWheel.Interop;

namespace DevWheel.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private HotkeyService? _hotkeys;
    private WheelController? _controller;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(initiallyOwned: true, @"Global\DevWheel", out var createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            MessageBox.Show("DevWheel is already running. Look for its icon in the system tray.", "DevWheel",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = store.Load();
        _hotkeys = new HotkeyService();
        var clipboard = new ClipboardService(_hotkeys.Handle);
        _controller = new WheelController(
            ToolRegistry.CreateDefault(TimeProvider.System), store, settings, _hotkeys, clipboard, new SelectionService(clipboard));
        _tray = new TrayIcon(_controller, ExitApp);
        _controller.Notify = _tray.ShowBalloon;
        _controller.Start();
    }

    private void ExitApp()
    {
        _controller?.Dispose();
        _tray?.Dispose();
        _hotkeys?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Never show or log the exception text: it may contain selected text or tool output (SEC-02).
        e.Handled = true;
        Toast.ShowError($"Unexpected error ({e.Exception.GetType().Name})");
    }
}
